[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $projectRoot '.env'
if (-not (Test-Path -LiteralPath $envPath)) {
    throw 'Missing .env. Copy .env.example to .env first.'
}
$settings = & (Join-Path $PSScriptRoot 'Read-DatabaseSettings.ps1') `
    -EnvPath $envPath
$env:MSSQL_SA_PASSWORD = $settings.Password

if ($PSCmdlet.ShouldProcess(
    'crankdemo-sqlserver and crankdemo-sqlserver-data',
    'Run podman compose down -v')) {
    Push-Location $projectRoot
    try {
        & podman compose down -v
        if ($LASTEXITCODE -ne 0) {
            throw "podman compose down -v failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}
