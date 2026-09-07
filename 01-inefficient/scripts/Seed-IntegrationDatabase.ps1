[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$containerRuntime = $env:PERFORMANCE_DEMO_CONTAINER_RUNTIME
if ([string]::IsNullOrWhiteSpace($containerRuntime)) {
    $containerRuntime = 'podman'
}

& $containerRuntime exec performancedemo-integration-sqlserver `
    /bin/bash -c @'
SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U "$SQLSERVER_USER" -C -b -I \
  -d "$SQLSERVER_DATABASE" -i /seed/integration/001-seed-data.sql
'@
if ($LASTEXITCODE -ne 0) {
    throw "Integration seed failed with exit code $LASTEXITCODE."
}
