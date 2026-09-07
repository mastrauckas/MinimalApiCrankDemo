[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$settings = & (Join-Path $PSScriptRoot 'Get-DatabaseSettings.ps1')
$databaseProject =
    '.\src\MinimalApiPerformanceDemo.Database\' +
    'MinimalApiPerformanceDemo.Database.csproj'
$env:ConnectionStrings__PerformanceDemo =
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
        $databaseProject --startup-project $databaseProject
    if ($LASTEXITCODE -ne 0) {
        throw "EF migration failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
