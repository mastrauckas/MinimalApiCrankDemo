[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $projectRoot '.env'

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

& podman info *> $null
if ($LASTEXITCODE -ne 0) {
    throw "Podman's machine is stopped. Run: podman machine start"
}

if (-not (Test-Path -LiteralPath $envPath)) {
    $generatedPassword = "Crank-$([Guid]::NewGuid().ToString('N'))-Aa1!"
    Set-Content -LiteralPath $envPath -Encoding utf8 -Value @(
        "MSSQL_SA_PASSWORD=$generatedPassword",
        'SQLSERVER_HOST=localhost',
        'SQLSERVER_PORT=14333',
        'SQLSERVER_DATABASE=CrankDemo',
        'SQLSERVER_USER=sa'
    )
}

Push-Location $projectRoot
try {
    & podman compose up -d sqlserver
    if ($LASTEXITCODE -ne 0) {
        throw "podman compose up failed with exit code $LASTEXITCODE."
    }

    $settings = & (Join-Path $projectRoot `
            'scripts/Read-DatabaseSettings.ps1') `
        -EnvPath $envPath

    $ready = $false
    for ($attempt = 1; $attempt -le 60; $attempt++) {
        & podman exec crankdemo-sqlserver `
            /opt/mssql-tools18/bin/sqlcmd -S localhost `
            -U $settings.UserName `
            -P $settings.Password -C -Q 'SELECT 1' *> $null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }

        Start-Sleep -Seconds 2
    }
    if (-not $ready) {
        throw 'SQL Server was not reachable within 120 seconds.'
    }

    & (Join-Path $projectRoot 'scripts/Invoke-Migrations.ps1')
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
