[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $ContainerName = 'crankdemo-sqlserver'
)

$ErrorActionPreference = 'Stop'
$containerRuntime = $env:CRANK_DEMO_CONTAINER_RUNTIME
if ([string]::IsNullOrWhiteSpace($containerRuntime)) {
    $containerRuntime = 'podman'
}

& $containerRuntime exec $ContainerName /bin/bash -c @'
SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U "$SQLSERVER_USER" -C -b -I \
  -d "$SQLSERVER_DATABASE" -i /seed/benchmark/001-seed-data.sql
'@
if ($LASTEXITCODE -ne 0) {
    throw "Benchmark seed failed with exit code $LASTEXITCODE."
}
