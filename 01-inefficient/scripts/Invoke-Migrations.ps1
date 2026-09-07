[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $ConnectionString,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $BundlePath = (Join-Path `
        (Split-Path -Parent $PSScriptRoot) `
        'artifacts/efbundle.exe')
)

$ErrorActionPreference = 'Stop'
$pathApi = $ExecutionContext.SessionState.Path
$resolvedBundlePath =
    $pathApi.GetUnresolvedProviderPathFromPSPath($BundlePath)

if (-not (Test-Path -LiteralPath $resolvedBundlePath -PathType Leaf)) {
    throw "Migration bundle not found: $resolvedBundlePath"
}

$connectionVariable = 'ConnectionStrings__PerformanceDemo'
$originalConnection = [Environment]::GetEnvironmentVariable(
    $connectionVariable,
    [EnvironmentVariableTarget]::Process)
$migrationExitCode = $null

try {
    # The design-time factory constructs the context before EF processes the
    # bundle's --connection override, so it also needs the in-memory value.
    $env:ConnectionStrings__PerformanceDemo = $ConnectionString
    & $resolvedBundlePath --connection $ConnectionString
    $migrationExitCode = $LASTEXITCODE
}
finally {
    if ($null -eq $originalConnection) {
        Remove-Item Env:ConnectionStrings__PerformanceDemo `
            -ErrorAction SilentlyContinue
    }
    else {
        $env:ConnectionStrings__PerformanceDemo = $originalConnection
    }
}

if ($migrationExitCode -ne 0) {
    throw "Migration bundle failed with exit code $migrationExitCode."
}
