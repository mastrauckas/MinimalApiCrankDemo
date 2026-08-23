[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $projectRoot '.env'
if (-not (Test-Path -LiteralPath $envPath)) {
    throw 'Missing .env. Copy .env.example to .env first.'
}

$settings = & (Join-Path $PSScriptRoot 'Read-DatabaseSettings.ps1') `
    -EnvPath $envPath

& podman exec crankdemo-sqlserver `
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U $settings.UserName `
    -P $settings.Password -C -b -d $settings.Database `
    -i /seed/integration/001-seed-data.sql
if ($LASTEXITCODE -ne 0) {
    throw "Integration seed failed with exit code $LASTEXITCODE."
}
