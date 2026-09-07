[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Podman', 'Docker')]
    [string] $ContainerRuntime = 'Podman',

    [Parameter()]
    [switch] $Cleanup
)

$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'Invoke-BenchmarkSetupAndCrank.ps1') `
    -ContainerRuntime $ContainerRuntime `
    -BenchmarkTool Siege `
    -Cleanup:$Cleanup

exit $LASTEXITCODE
