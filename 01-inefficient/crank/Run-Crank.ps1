[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

function Install-OrUpdateTool {
    param(
        [Parameter(Mandatory)][string] $Package,
        [Parameter(Mandatory)][string] $Command
    )

    $installed = dotnet tool list --global |
        Select-String -SimpleMatch $Package
    if ($installed) {
        dotnet tool update $Package --global --version '0.2.0-*'
    }
    else {
        dotnet tool install $Package --global --version '0.2.0-*'
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install or update $Command."
    }
}

function Get-ExistingDatabasePassword {
    $containerEnvironment = & podman inspect `
        --format '{{range .Config.Env}}{{println .}}{{end}}' `
        crankdemo-sqlserver 2> $null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    $passwordEntry = $containerEnvironment |
        Where-Object { $_ -like 'MSSQL_SA_PASSWORD=*' } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($passwordEntry)) {
        return $null
    }

    return $passwordEntry.Substring('MSSQL_SA_PASSWORD='.Length)
}

& podman info *> $null
if ($LASTEXITCODE -ne 0) {
    throw "Podman's machine is stopped. Run: podman machine start"
}

$databasePassword = Get-ExistingDatabasePassword
if ([string]::IsNullOrWhiteSpace($databasePassword)) {
    $databasePassword =
        "Crank-$([Guid]::NewGuid().ToString('N'))-Aa1!"

    & podman volume exists crankdemo-sqlserver-data
    if ($LASTEXITCODE -eq 0) {
        & podman volume rm crankdemo-sqlserver-data *> $null
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not remove the orphaned benchmark volume.'
        }
    }
}
$env:CRANK_DEMO_CONTAINER_RUNTIME = 'podman'
$env:MSSQL_SA_PASSWORD = $databasePassword
$env:SQLSERVER_HOST = 'localhost'
$env:SQLSERVER_PORT = '14333'
$env:SQLSERVER_DATABASE = 'CrankDemo'
$env:SQLSERVER_USER = 'sa'

Push-Location $projectRoot
try {
    & podman compose up -d sqlserver
    if ($LASTEXITCODE -ne 0) {
        throw "podman compose up failed with exit code $LASTEXITCODE."
    }

    $ready = $false
    for ($attempt = 1; $attempt -le 60; $attempt++) {
        & podman exec crankdemo-sqlserver /bin/bash -c @'
SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U "$SQLSERVER_USER" -C -Q "SELECT 1"
'@ *> $null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }

        Start-Sleep -Seconds 2
    }
    if (-not $ready) {
        throw 'SQL Server was not reachable within 120 seconds.'
    }

    & (Join-Path $projectRoot 'scripts/Invoke-LocalMigrations.ps1')
    & (Join-Path $projectRoot 'scripts/Seed-BenchmarkDatabase.ps1')
}
finally {
    Pop-Location
}

Install-OrUpdateTool 'Microsoft.Crank.Controller' 'crank'
Install-OrUpdateTool 'Microsoft.Crank.Agent' 'crank-agent'

$apiProcess = $null
$agentProcess = $null
try {
    try {
        Invoke-RestMethod -Uri 'http://localhost:8640/health/live' `
            -TimeoutSec 2 | Out-Null
    }
    catch {
        $apiProcess = Start-Process -FilePath 'pwsh' `
            -ArgumentList '-NoProfile', '-File',
                (Join-Path $projectRoot 'scripts/Start-Api.ps1') `
            -WindowStyle Hidden -PassThru

        $apiReady = $false
        for ($attempt = 1; $attempt -le 30; $attempt++) {
            try {
                Invoke-RestMethod `
                    -Uri 'http://localhost:8640/health/live' `
                    -TimeoutSec 2 | Out-Null
                $apiReady = $true
                break
            }
            catch {
                Start-Sleep -Seconds 1
            }
        }
        if (-not $apiReady) {
            throw 'The API was not reachable within 30 seconds.'
        }
    }

    $loginBody = @{
        email = 'demo@example.com'
        password = 'DemoPassword123!'
    } | ConvertTo-Json
    $loginResponse = Invoke-RestMethod `
        -Uri 'http://localhost:8640/api/auth/login' `
        -Method Post -ContentType 'application/json' -Body $loginBody
    $bearerToken = $loginResponse.accessToken
    if ([string]::IsNullOrWhiteSpace($bearerToken)) {
        throw 'The Identity login response did not contain an access token.'
    }

    try {
        Invoke-WebRequest -Uri 'http://localhost:5010' `
            -Method Head -TimeoutSec 2 | Out-Null
    }
    catch {
        $agentProcess = Start-Process -FilePath 'crank-agent' `
            -ArgumentList '--url', 'http://localhost:5010' `
            -WindowStyle Hidden -PassThru
        Start-Sleep -Seconds 3
    }

    crank --config (Join-Path $PSScriptRoot 'crank.yml') `
        --scenario products --profile local `
        --variable "bearerToken=$bearerToken"
    if ($LASTEXITCODE -ne 0) {
        throw "Crank failed with exit code $LASTEXITCODE."
    }
}
finally {
    if ($agentProcess) {
        Stop-Process -Id $agentProcess.Id -ErrorAction SilentlyContinue
    }
    if ($apiProcess) {
        Stop-Process -Id $apiProcess.Id -ErrorAction SilentlyContinue
    }
}
