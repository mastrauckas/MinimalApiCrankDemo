[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $projectRoot '.env'
if (-not (Test-Path -LiteralPath $envPath)) {
    throw 'Missing .env. Run the integration tests once or copy .env.example.'
}

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
    (Join-Path $projectRoot 'src/MinimalApiCrankDemo.Api') `
    --no-launch-profile -- --seed-demo-data
if ($LASTEXITCODE -ne 0) {
    throw "Demo data seed failed with exit code $LASTEXITCODE."
}
