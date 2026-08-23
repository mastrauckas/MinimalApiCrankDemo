[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $projectRoot `
    'docker-compose.integration-tests.yml'
$containerName = 'crankdemo-integration-sqlserver'
$volumeName = 'crankdemo-integration-sqlserver-data'
$networkName = 'minimal-api-crank-demo-integration-tests_default'
$containerRuntime = $env:CRANK_DEMO_CONTAINER_RUNTIME
if ([string]::IsNullOrWhiteSpace($containerRuntime)) {
    $containerRuntime = 'podman'
}
if ($containerRuntime -notin @('podman', 'docker')) {
    throw 'CRANK_DEMO_CONTAINER_RUNTIME must be podman or docker.'
}

Push-Location $projectRoot
try {
    & $containerRuntime compose --file $composeFile `
        down -v --remove-orphans *> $null

    # Compose providers can return while Podman is still finishing startup.
    # Force removal by the test-only names so failure cleanup is deterministic.
    & $containerRuntime rm --force $containerName *> $null
    & $containerRuntime volume rm --force $volumeName *> $null
    & $containerRuntime network rm --force $networkName *> $null

    & $containerRuntime container inspect $containerName *> $null
    if ($LASTEXITCODE -eq 0) {
        throw "Could not remove test container $containerName."
    }

    & $containerRuntime volume inspect $volumeName *> $null
    if ($LASTEXITCODE -eq 0) {
        throw "Could not remove test volume $volumeName."
    }

    & $containerRuntime network inspect $networkName *> $null
    if ($LASTEXITCODE -eq 0) {
        throw "Could not remove test network $networkName."
    }
}
finally {
    Pop-Location
}
