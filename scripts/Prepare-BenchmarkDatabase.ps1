[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

& (Join-Path $PSScriptRoot 'Prepare-IntegrationDatabase.ps1')

$envPath = Join-Path $projectRoot '.env'
$passwordLine = Get-Content -LiteralPath $envPath |
    Where-Object { $_ -match '^MSSQL_SA_PASSWORD=' } |
    Select-Object -First 1
if (-not $passwordLine) {
    throw '.env must define MSSQL_SA_PASSWORD.'
}

$password = $passwordLine.Substring('MSSQL_SA_PASSWORD='.Length)
$env:ConnectionStrings__CrankDemo =
    "Server=localhost,14333;Database=CrankDemo;User ID=sa;" +
    "Password=$password;TrustServerCertificate=True"

dotnet run --project `
    (Join-Path $projectRoot `
        'tools/MinimalApiCrankDemo.BenchmarkSetup') `
    --no-launch-profile
if ($LASTEXITCODE -ne 0) {
    throw "Benchmark seed failed with exit code $LASTEXITCODE."
}
