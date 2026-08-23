[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter()]
    [ValidateSet('Podman', 'Docker')]
    [string] $ContainerRuntime = 'Podman'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $projectRoot 'docker-compose.benchmark.yml'
$runtimeCommand = $ContainerRuntime.ToLowerInvariant()
$containerName = 'crankdemo-benchmark-sqlserver'
$volumeName = 'crankdemo-benchmark-sqlserver-data'

if (-not $PSCmdlet.ShouldProcess(
    "$containerName and $volumeName",
    'Remove the benchmark SQL Server container and volume')) {
    return
}

& $runtimeCommand compose --file $composeFile `
    down -v --remove-orphans *> $null
if ($LASTEXITCODE -ne 0) {
    throw "$ContainerRuntime Compose cleanup failed."
}

# Remove resources left by an interrupted Compose run.
& $runtimeCommand rm --force $containerName *> $null
& $runtimeCommand volume rm --force $volumeName *> $null

& $runtimeCommand container inspect $containerName *> $null
if ($LASTEXITCODE -eq 0) {
    throw "Could not remove benchmark container $containerName."
}

& $runtimeCommand volume inspect $volumeName *> $null
if ($LASTEXITCODE -eq 0) {
    throw "Could not remove benchmark volume $volumeName."
}

Write-Host 'Removed the benchmark SQL Server container and volume.'
