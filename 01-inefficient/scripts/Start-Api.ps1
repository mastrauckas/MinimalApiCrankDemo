[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$settings = & (Join-Path $PSScriptRoot 'Get-DatabaseSettings.ps1')
$env:ConnectionStrings__PerformanceDemo =
    "Server=$($settings.HostName),$($settings.Port);" +
    "Database=$($settings.Database);User ID=$($settings.UserName);" +
    "Password=$($settings.Password);TrustServerCertificate=True"

dotnet run --project `
    (Join-Path $projectRoot 'src/MinimalApiPerformanceDemo.Api') `
    --launch-profile http
