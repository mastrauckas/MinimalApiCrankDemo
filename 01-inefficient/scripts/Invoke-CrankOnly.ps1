[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Podman', 'Docker')]
    [string] $ContainerRuntime = 'Podman'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiBaseAddress = 'http://127.0.0.1:8640'
$healthAddress = "$apiBaseAddress/health/live"
$runtimeCommand = $ContainerRuntime.ToLowerInvariant()
$containerName = 'crankdemo-benchmark-sqlserver'
$apiProcess = $null
$environmentNames = @(
    'MSSQL_SA_PASSWORD',
    'SQLSERVER_HOST',
    'SQLSERVER_PORT',
    'SQLSERVER_DATABASE',
    'SQLSERVER_USER',
    'ConnectionStrings__CrankDemo'
)
$originalEnvironment = @{}
$timestamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$resultPath = Join-Path $projectRoot `
    "artifacts/crank/inefficient-products-$timestamp.json"

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

    # Inspect output is captured in memory and is never printed because it
    # contains the disposable benchmark SQL Server password.
    $inspectOutput = & $runtimeCommand inspect $containerName 2> $null
    if ($LASTEXITCODE -ne 0) {
        throw (
            "The dedicated benchmark container '$containerName' does not " +
            'exist. Run Invoke-BenchmarkSetupAndCrank.ps1 once to prepare it.')
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
    $env:SQLSERVER_DATABASE = 'CrankDemoBenchmark'
    $env:SQLSERVER_USER = 'sa'
    $env:ConnectionStrings__CrankDemo =
        'Server=127.0.0.1,14334;Database=CrankDemoBenchmark;' +
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

foreach ($name in $environmentNames) {
    $originalEnvironment[$name] =
        [Environment]::GetEnvironmentVariable($name)
}

try {
    if (-not (Test-ApiHealth)) {
        Start-ExistingBenchmarkApi
    }

    # These are the documented, non-secret credentials for disposable data.
    $loginBody = @{
        email = 'demo@example.com'
        password = 'DemoPassword123!'
    } | ConvertTo-Json
    $loginResponse = Invoke-RestMethod `
        -Uri "$apiBaseAddress/api/auth/login" `
        -Method Post `
        -ContentType 'application/json' `
        -Body $loginBody
    $bearerToken = $loginResponse.accessToken
    if ([string]::IsNullOrWhiteSpace($bearerToken)) {
        throw 'The Identity login response did not contain an access token.'
    }

    & (Join-Path $projectRoot 'crank/Run-Crank.ps1') `
        -BearerToken $bearerToken `
        -ResultPath $resultPath
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

Write-Host (
    "Benchmark: products; API: $apiBaseAddress; " +
    "runtime selection: $ContainerRuntime")
