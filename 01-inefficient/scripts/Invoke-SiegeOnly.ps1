[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Podman', 'Docker')]
    [string] $ContainerRuntime = 'Podman',

    [Parameter()]
    [ValidateRange(1, 1024)]
    [int] $Connections = 32,

    [Parameter()]
    [ValidateRange(0, 3600)]
    [int] $WarmupSeconds = 5,

    [Parameter()]
    [ValidateRange(1, 3600)]
    [int] $DurationSeconds = 15
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$siegeDirectory = Join-Path $projectRoot 'benchmarks/siege'
$apiBaseAddress = 'http://127.0.0.1:8640'
$healthAddress = "$apiBaseAddress/health/live"
$runtimeCommand = $ContainerRuntime.ToLowerInvariant()
$containerName = 'performancedemo-benchmark-sqlserver'
$imageName = 'minimal-api-performance-demo-siege:local'
$apiProcess = $null
$environmentNames = @(
    'MSSQL_SA_PASSWORD',
    'SQLSERVER_HOST',
    'SQLSERVER_PORT',
    'SQLSERVER_DATABASE',
    'SQLSERVER_USER',
    'ConnectionStrings__PerformanceDemo'
)
$originalEnvironment = @{}
$timestamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$resultPath = Join-Path $projectRoot `
    "artifacts/benchmarks/siege/inefficient-products-$timestamp.txt"

function Test-ApiHealth {
    try {
        Invoke-RestMethod -Uri $healthAddress -TimeoutSec 2 | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Start-ExistingBenchmarkApi {
    if ($runtimeCommand -eq 'podman') {
        & podman info *> $null
        if ($LASTEXITCODE -ne 0) {
            throw "Podman's machine is stopped. Run: podman machine start"
        }
    }
    else {
        & docker version *> $null
        if ($LASTEXITCODE -ne 0) {
            throw 'Docker Desktop or Docker Engine is not running.'
        }
    }

    $inspectOutput = & $runtimeCommand inspect $containerName 2> $null
    if ($LASTEXITCODE -ne 0) {
        throw (
            "The dedicated benchmark container '$containerName' does not " +
            'exist. Run Invoke-BenchmarkSetupAndSiege.ps1 once to prepare it.')
    }

    $container = @($inspectOutput | Out-String | ConvertFrom-Json)[0]
    $passwordEntry = $container.Config.Env |
        Where-Object { $_ -like 'MSSQL_SA_PASSWORD=*' } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($passwordEntry)) {
        throw 'The benchmark container has no SQL Server password setting.'
    }

    $env:MSSQL_SA_PASSWORD =
        $passwordEntry.Substring('MSSQL_SA_PASSWORD='.Length)
    $env:SQLSERVER_HOST = '127.0.0.1'
    $env:SQLSERVER_PORT = '14334'
    $env:SQLSERVER_DATABASE = 'PerformanceDemoBenchmark'
    $env:SQLSERVER_USER = 'sa'
    $env:ConnectionStrings__PerformanceDemo =
        'Server=127.0.0.1,14334;Database=PerformanceDemoBenchmark;' +
        'User ID=sa;' +
        "Password=$($env:MSSQL_SA_PASSWORD);" +
        'TrustServerCertificate=True'

    if (-not $container.State.Running) {
        & $runtimeCommand start $containerName | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to start benchmark container '$containerName'."
        }
    }

    $script:apiProcess = Start-Process -FilePath 'pwsh' `
        -ArgumentList '-NoProfile', '-File',
            (Join-Path $PSScriptRoot 'Start-Api.ps1') `
        -WindowStyle Hidden -PassThru

    for ($attempt = 1; $attempt -le 60; $attempt++) {
        if (Test-ApiHealth) {
            return
        }
        if ($script:apiProcess.HasExited) {
            throw 'The benchmark API exited before becoming healthy.'
        }
        Start-Sleep -Seconds 1
    }

    throw "The API was not healthy at $healthAddress within 60 seconds."
}

function Build-SiegeImage {
    $inspectOutput = & $runtimeCommand image inspect $imageName 2> $null
    if ($LASTEXITCODE -eq 0) {
        return
    }

    & $runtimeCommand build --tag $imageName $siegeDirectory
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to build the local Siege benchmark image.'
    }
}

function Invoke-SiegeRun {
    param([Parameter(Mandatory)][int] $Seconds)

    $benchmarkHost = if ($runtimeCommand -eq 'podman') {
        $wslGateway = Get-NetIPAddress -AddressFamily IPv4 |
            Where-Object {
                $_.InterfaceAlias -like 'vEthernet (WSL*' -and
                $_.AddressState -eq 'Preferred'
            } |
            Select-Object -First 1 -ExpandProperty IPAddress
        if ([string]::IsNullOrWhiteSpace($wslGateway)) {
            throw (
                'Podman on Windows requires an active WSL virtual-network ' +
                'adapter to reach the API. Start the Podman machine and retry.')
        }

        $wslGateway
    }
    else {
        'host.docker.internal'
    }
    $targetUrl = "http://${benchmarkHost}:8640/api/products"
    $output = & $runtimeCommand run --rm $imageName `
        --benchmark `
        --concurrent=$Connections `
        --time="$($Seconds)S" `
        --header "Authorization: Bearer $script:bearerToken" `
        $targetUrl 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Siege failed with exit code $LASTEXITCODE."
    }

    $lines = @($output | ForEach-Object { $_.ToString() })
    $summary = $lines -join [Environment]::NewLine
    if ($summary -notmatch '"successful_transactions":\s+([1-9]\d*)' -or
        $summary -match '"failed_transactions":\s+([1-9]\d*)') {
        throw (
            'Siege completed without a successful, error-free measurement. ' +
            'Confirm that the API is reachable from the benchmark container.')
    }

    return $lines
}

foreach ($name in $environmentNames) {
    $originalEnvironment[$name] =
        [Environment]::GetEnvironmentVariable($name)
}

try {
    if (-not (Test-ApiHealth)) {
        Start-ExistingBenchmarkApi
    }

    $loginBody = @{
        email = 'demo@example.com'
        password = 'DemoPassword123!'
    } | ConvertTo-Json
    $loginResponse = Invoke-RestMethod `
        -Uri "$apiBaseAddress/api/auth/login" `
        -Method Post `
        -ContentType 'application/json' `
        -Body $loginBody
    $script:bearerToken = $loginResponse.accessToken
    if ([string]::IsNullOrWhiteSpace($script:bearerToken)) {
        throw 'The Identity login response did not contain an access token.'
    }

    Build-SiegeImage
    if ($WarmupSeconds -gt 0) {
        [void] (Invoke-SiegeRun -Seconds $WarmupSeconds)
    }

    $measurement = Invoke-SiegeRun -Seconds $DurationSeconds
    New-Item -ItemType Directory -Path (Split-Path -Parent $resultPath) `
        -Force | Out-Null
    [IO.File]::WriteAllLines($resultPath, $measurement)
    $measurement | Write-Host
    Write-Host "Siege results: $resultPath"
}
finally {
    if ($apiProcess) {
        & taskkill.exe /PID $apiProcess.Id /T /F *> $null
    }

    foreach ($name in $environmentNames) {
        $originalValue = $originalEnvironment[$name]
        if ($null -eq $originalValue) {
            [Environment]::SetEnvironmentVariable($name, $null)
        }
        else {
            [Environment]::SetEnvironmentVariable($name, $originalValue)
        }
    }
}
