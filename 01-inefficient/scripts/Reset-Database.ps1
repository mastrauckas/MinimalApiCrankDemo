[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

if ($PSCmdlet.ShouldProcess(
    'crankdemo-sqlserver and crankdemo-sqlserver-data',
    'Run podman compose down -v')) {
    Push-Location $projectRoot
    try {
        & podman compose down -v
        if ($LASTEXITCODE -ne 0) {
            throw "podman compose down -v failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}
