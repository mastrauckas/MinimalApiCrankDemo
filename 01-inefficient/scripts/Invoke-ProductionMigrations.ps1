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
$migrationScript = Join-Path $PSScriptRoot 'Invoke-Migrations.ps1'

& $migrationScript `
    -ConnectionString $ConnectionString `
    -BundlePath $BundlePath
