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

Install-OrUpdateTool 'Microsoft.Crank.Controller' 'crank'
Install-OrUpdateTool 'Microsoft.Crank.Agent' 'crank-agent'

& (Join-Path $projectRoot 'scripts/Prepare-BenchmarkDatabase.ps1')

$loginBody = @{
    email = 'demo@example.com'
    password = 'DemoPassword123!'
} | ConvertTo-Json
try {
    $loginResponse = Invoke-RestMethod `
        -Uri 'http://localhost:8640/api/auth/login' `
        -Method Post -ContentType 'application/json' -Body $loginBody
}
catch {
    throw 'Could not obtain an Identity bearer token. Start the API first.'
}
$bearerToken = $loginResponse.accessToken
if ([string]::IsNullOrWhiteSpace($bearerToken)) {
    throw 'The Identity login response did not contain an access token.'
}

$agentProcess = $null
try {
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
}
