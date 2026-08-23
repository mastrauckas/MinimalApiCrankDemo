[CmdletBinding()]
param()

# The integration fixture owns this disposable container and its data.

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

function Invoke-Compose {
    param([Parameter(Mandatory)][string[]] $Arguments)

    & $containerRuntime compose --file $composeFile @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw (
            "$containerRuntime compose $($Arguments -join ' ') failed " +
            "with exit code $LASTEXITCODE.")
    }
}

function Invoke-ContainerRuntime {
    param([Parameter(Mandatory)][string[]] $Arguments)

    & $containerRuntime @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw (
            "$containerRuntime $($Arguments -join ' ') failed with " +
            "exit code $LASTEXITCODE.")
    }
}

function Remove-TestResources {
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

function Test-ContainerRunning {
    $state = & $containerRuntime inspect $containerName `
        --format '{{.State.Running}}' 2> $null
    return $LASTEXITCODE -eq 0 -and $state -eq 'true'
}

if ($containerRuntime -eq 'podman') {
    & podman info *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Podman's machine is stopped. Run: podman machine start"
    }
}
else {
    & docker version *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker Desktop or Docker Engine is not running.'
    }
}

$settings = & (Join-Path $PSScriptRoot 'Get-DatabaseSettings.ps1')

Push-Location $projectRoot
try {
    # A test run always starts with a new container and named volume.
    Invoke-Compose @('down', '-v', '--remove-orphans')
    Remove-TestResources
    Invoke-Compose @('up', '-d', 'sqlserver')

    $connectionStringBuilder =
        [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $connectionStringBuilder.set_DataSource(
        "$($settings.HostName),$($settings.Port)")
    $connectionStringBuilder.set_InitialCatalog('master')
    $connectionStringBuilder.set_UserID($settings.UserName)
    $connectionStringBuilder.set_Password($settings.Password)
    $connectionStringBuilder.set_Encrypt($true)
    $connectionStringBuilder.set_TrustServerCertificate($true)
    $connectionStringBuilder.set_ConnectTimeout(3)
    $connectionStringBuilder.set_ConnectRetryCount(0)
    $connectionStringBuilder.set_Pooling($false)

    # Authenticate from Windows through the same endpoint EF Core uses.
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    $ready = $false
    $successfulProbes = 0
    $lastConnectionError = 'No connection attempt completed.'
    while ([DateTime]::UtcNow -lt $deadline) {
        if (-not (Test-ContainerRunning)) {
            $lastConnectionError = 'The new test container is not running.'
            Start-Sleep -Seconds 2
            continue
        }

        $connection = [System.Data.SqlClient.SqlConnection]::new(
            $connectionStringBuilder.ConnectionString)
        try {
            $connection.Open()
            $command = $connection.CreateCommand()
            $command.CommandText = 'SELECT 1'
            $command.CommandTimeout = 3
            [void] $command.ExecuteScalar()
            if (Test-ContainerRunning) {
                $successfulProbes++
                if ($successfulProbes -ge 2) {
                    $ready = $true
                    break
                }
            }
        }
        catch {
            $successfulProbes = 0
            $lastConnectionError = $_.Exception.Message
        }
        finally {
            $connection.Dispose()
        }

        Start-Sleep -Seconds 2
    }

    if (-not $ready) {
        $composeStatus = (
            & $containerRuntime compose --file $composeFile ps 2>&1
        ) -join [Environment]::NewLine
        throw (
            'SQL Server was not reachable from Windows at ' +
            "$($settings.HostName):$($settings.Port) within 120 seconds." +
            [Environment]::NewLine +
            "Last connection error: $lastConnectionError" +
            [Environment]::NewLine + [Environment]::NewLine +
            "$containerRuntime compose ps:" +
            [Environment]::NewLine + $composeStatus +
            [Environment]::NewLine +
            'Inspect SQL Server logs with: ' +
            "$containerRuntime compose --file " +
            "docker-compose.integration-tests.yml logs sqlserver")
    }

    Invoke-ContainerRuntime @(
        'exec', $containerName, '/bin/bash', '-c', @'
SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U "$SQLSERVER_USER" -C -b \
  -d master -i /seed/001-recreate-database.sql
'@
    )

    & (Join-Path $PSScriptRoot 'Invoke-Migrations.ps1')

    # Migrations create schema only. Test data must still be absent here.
    Invoke-ContainerRuntime @(
        'exec', $containerName, '/bin/bash', '-c', @'
SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U "$SQLSERVER_USER" -C -b \
  -d "$SQLSERVER_DATABASE" \
  -Q "IF EXISTS (SELECT 1 FROM dbo.AspNetUsers)
      THROW 51000, 'Migration inserted demo data.', 1;"
'@
    )

    & (Join-Path $PSScriptRoot 'Seed-IntegrationDatabase.ps1')

    # The second pass proves the SQL seed remains idempotent.
    & (Join-Path $PSScriptRoot 'Seed-IntegrationDatabase.ps1')
}
finally {
    Pop-Location
}
