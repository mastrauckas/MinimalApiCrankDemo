[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z._-]*$')]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$databaseProject = Join-Path $projectRoot `
    'src/MinimalApiPerformanceDemo.Database'
$bundleDirectory = Join-Path $projectRoot `
    "artifacts/migration-bundles/$Version"
$bundlePath = Join-Path $bundleDirectory 'efbundle.exe'
$connectionVariable = 'ConnectionStrings__PerformanceDemo'
$originalConnection = [Environment]::GetEnvironmentVariable(
    $connectionVariable,
    [EnvironmentVariableTarget]::Process)

New-Item -ItemType Directory -Path $bundleDirectory -Force | Out-Null

Push-Location $projectRoot
try {
    # EF requires provider options at design time but does not connect here.
    $env:ConnectionStrings__PerformanceDemo =
        'Server=unused;Database=PerformanceDemo;Integrated Security=True;Encrypt=True'

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed with exit code $LASTEXITCODE."
    }

    dotnet tool run dotnet-ef migrations bundle `
        --project $databaseProject `
        --startup-project $databaseProject `
        --output $bundlePath `
        --force
    if ($LASTEXITCODE -ne 0) {
        throw (
            'Migration bundle build failed with exit code ' +
            "$LASTEXITCODE.")
    }
}
finally {
    if ($null -eq $originalConnection) {
        Remove-Item Env:ConnectionStrings__PerformanceDemo `
            -ErrorAction SilentlyContinue
    }
    else {
        $env:ConnectionStrings__PerformanceDemo = $originalConnection
    }

    Pop-Location
}

Write-Output $bundlePath
