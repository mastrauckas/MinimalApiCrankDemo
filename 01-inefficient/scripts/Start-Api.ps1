[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $projectRoot '.env'
if (-not (Test-Path -LiteralPath $envPath)) {
    throw 'Missing .env. Run the integration tests once or copy .env.example.'
}

$settings = & (Join-Path $PSScriptRoot 'Read-DatabaseSettings.ps1') `
    -EnvPath $envPath
$env:ConnectionStrings__CrankDemo =
    "Server=$($settings.HostName),$($settings.Port);" +
    "Database=$($settings.Database);User ID=$($settings.UserName);" +
    "Password=$($settings.Password);TrustServerCertificate=True"

dotnet run --project `
    (Join-Path $projectRoot 'src/MinimalApiCrankDemo.Api') `
    --launch-profile http
