[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Podman', 'Docker')]
    [string] $ContainerRuntime = 'Podman',

    [Parameter()]
    [ValidateSet('Crank', 'Siege')]
    [string] $BenchmarkTool = 'Crank',

    [Parameter()]
    [switch] $Cleanup
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $projectRoot 'docker-compose.benchmark.yml'
$runtimeCommand = $ContainerRuntime.ToLowerInvariant()
$containerName = 'performancedemo-benchmark-sqlserver'
$databaseName = 'PerformanceDemoBenchmark'
$databaseHost = '127.0.0.1'
$databasePort = '14334'
$databaseUser = 'sa'
$apiAddress = 'http://127.0.0.1:8640'
$apiProcess = $null
$benchmarkStarted = $false
$runtimeAvailable = $false
$environmentNames = @(
    'PERFORMANCE_DEMO_CONTAINER_RUNTIME',
    'MSSQL_SA_PASSWORD',
    'SQLSERVER_HOST',
    'SQLSERVER_PORT',
    'SQLSERVER_DATABASE',
    'SQLSERVER_USER',
    'ConnectionStrings__PerformanceDemo'
)
$originalEnvironment = @{}

function Invoke-Compose {
    param([Parameter(Mandatory)][string[]] $Arguments)

    & $runtimeCommand compose --file $composeFile @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw (
            "$runtimeCommand compose $($Arguments -join ' ') failed " +
            "with exit code $LASTEXITCODE.")
    }
}

function Remove-BenchmarkDatabase {
    & (Join-Path $PSScriptRoot 'Remove-BenchmarkDatabase.ps1') `
        -ContainerRuntime $ContainerRuntime
}

function Wait-ForSqlServer {
    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder.set_DataSource("$databaseHost,$databasePort")
    $builder.set_InitialCatalog('master')
    $builder.set_UserID($databaseUser)
    $builder.set_Password($env:MSSQL_SA_PASSWORD)
    $builder.set_Encrypt($true)
    $builder.set_TrustServerCertificate($true)
    $builder.set_ConnectTimeout(3)
    $builder.set_ConnectRetryCount(0)
    $builder.set_Pooling($false)

    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    $lastError = 'No connection attempt completed.'
    while ([DateTime]::UtcNow -lt $deadline) {
        $connection = [System.Data.SqlClient.SqlConnection]::new(
            $builder.ConnectionString)
        try {
            $connection.Open()
            $command = $connection.CreateCommand()
            $command.CommandText = 'SELECT 1'
            $command.CommandTimeout = 3
            [void] $command.ExecuteScalar()
            return
        }
        catch {
            $lastError = $_.Exception.Message
        }
        finally {
            $connection.Dispose()
        }

        Start-Sleep -Seconds 2
    }

    throw (
        "SQL Server authentication at ${databaseHost}:$databasePort " +
        "failed within 120 seconds. Last error: $lastError")
}

function Wait-ForApi {
    $healthAddress = "$apiAddress/health/live"
    for ($attempt = 1; $attempt -le 60; $attempt++) {
        try {
            Invoke-RestMethod -Uri $healthAddress `
                -TimeoutSec 2 | Out-Null
            return
        }
        catch {
            if ($apiProcess.HasExited) {
                throw 'The benchmark API exited before becoming healthy.'
            }
            Start-Sleep -Seconds 1
        }
    }

    throw "The API was not healthy at $healthAddress within 60 seconds."
}

foreach ($name in $environmentNames) {
    $originalEnvironment[$name] =
        [Environment]::GetEnvironmentVariable($name)
}

try {
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

    & $runtimeCommand compose version *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "$ContainerRuntime Compose is not available."
    }
    $runtimeAvailable = $true

    $passwordBytes =
        [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
    $env:PERFORMANCE_DEMO_CONTAINER_RUNTIME = $runtimeCommand
    $env:MSSQL_SA_PASSWORD =
        "Aa1!$([Convert]::ToHexString($passwordBytes))"
    $env:SQLSERVER_HOST = $databaseHost
    $env:SQLSERVER_PORT = $databasePort
    $env:SQLSERVER_DATABASE = $databaseName
    $env:SQLSERVER_USER = $databaseUser

    Push-Location $projectRoot
    try {
        Remove-BenchmarkDatabase
        Invoke-Compose @('up', '-d', 'sqlserver')
        $benchmarkStarted = $true
        Wait-ForSqlServer

        # Local Application Control policies may block generated bundle EXEs.
        # Use the project-based EF runner for this local benchmark workflow.
        & (Join-Path $PSScriptRoot 'Invoke-LocalMigrations.ps1')
        & (Join-Path $PSScriptRoot 'Seed-BenchmarkDatabase.ps1') `
            -ContainerName $containerName

        try {
            Invoke-RestMethod -Uri "$apiAddress/health/live" `
                -TimeoutSec 2 | Out-Null
            throw (
                "An API is already running at $apiAddress. Stop it or use " +
                "Invoke-$BenchmarkTool`Only.ps1.")
        }
        catch {
            if ($_.Exception.Message -like 'An API is already running*') {
                throw
            }
        }

        $apiProcess = Start-Process -FilePath 'pwsh' `
            -ArgumentList '-NoProfile', '-File',
                (Join-Path $PSScriptRoot 'Start-Api.ps1') `
            -WindowStyle Hidden -PassThru
        Wait-ForApi

        & (Join-Path $PSScriptRoot "Invoke-$BenchmarkTool`Only.ps1") `
            -ContainerRuntime $ContainerRuntime
    }
    finally {
        Pop-Location
    }
}
catch {
    if ($benchmarkStarted) {
        Write-Warning 'Benchmark SQL Server status:'
        & $runtimeCommand compose --file $composeFile ps 2>&1 |
            Write-Warning
        Write-Warning (
            'Inspect SQL Server logs with: ' +
            "$runtimeCommand compose --file " +
            "docker-compose.benchmark.yml logs sqlserver")
    }
    throw
}
finally {
    if ($apiProcess) {
        # Stop the PowerShell host and its dotnet child process.
        & taskkill.exe /PID $apiProcess.Id /T /F *> $null
    }

    if ($Cleanup -and $runtimeAvailable) {
        Remove-BenchmarkDatabase
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
    "Benchmark completed: runtime=$ContainerRuntime; SQL=" +
    "${databaseHost}:$databasePort/$databaseName; API=$apiAddress; " +
    "cleanup=$Cleanup")
