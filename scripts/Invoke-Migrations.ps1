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

Push-Location $projectRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed with exit code $LASTEXITCODE."
    }

    dotnet tool run dotnet-ef database update --project `
        '.\src\MinimalApiCrankDemo.Database\MinimalApiCrankDemo.Database.csproj' `
        --startup-project `
        '.\src\MinimalApiCrankDemo.Database\MinimalApiCrankDemo.Database.csproj'
    if ($LASTEXITCODE -ne 0) {
        throw "EF migration failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
