[CmdletBinding()]
param()

# Infrastructure and schema only; the integration fixture owns data seeding.

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $projectRoot '.env'

function Invoke-Podman {
    param([Parameter(Mandatory)][string[]] $Arguments)

    & podman @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "podman $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

& podman info *> $null
if ($LASTEXITCODE -ne 0) {
    throw "Podman's machine is stopped. Run: podman machine start"
}

if (-not (Test-Path -LiteralPath $envPath)) {
    $generatedPassword = "Crank-$([Guid]::NewGuid().ToString('N'))-Aa1!"
    Set-Content -LiteralPath $envPath -Encoding utf8 -Value @(
        "MSSQL_SA_PASSWORD=$generatedPassword",
        'SQLSERVER_HOST=localhost',
        'SQLSERVER_PORT=14333',
        'SQLSERVER_DATABASE=CrankDemo',
        'SQLSERVER_USER=sa'
    )
}

$settings = & (Join-Path $PSScriptRoot 'Read-DatabaseSettings.ps1') `
    -EnvPath $envPath

Push-Location $projectRoot
try {
    Invoke-Podman @('compose', 'up', '-d', 'sqlserver')

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

    # EF Core connects from Windows through the published host port. A
    # container-internal check can pass before that port forwarding is ready.
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    $ready = $false
    $lastConnectionError = 'No connection attempt completed.'
    while ([DateTime]::UtcNow -lt $deadline) {
        $connection = [System.Data.SqlClient.SqlConnection]::new(
            $connectionStringBuilder.ConnectionString)
        try {
            $connection.Open()
            $command = $connection.CreateCommand()
            $command.CommandText = 'SELECT 1'
            $command.CommandTimeout = 3
            [void] $command.ExecuteScalar()
            $ready = $true
            break
        }
        catch {
            $lastConnectionError = $_.Exception.Message
        }
        finally {
            $connection.Dispose()
        }

        Start-Sleep -Seconds 2
    }

    if (-not $ready) {
        $composeStatus = (& podman compose ps 2>&1) -join `
            [Environment]::NewLine
        throw (
            'SQL Server was not reachable from Windows at ' +
            "$($settings.HostName):$($settings.Port) within 120 seconds." +
            [Environment]::NewLine +
            "Last connection error: $lastConnectionError" +
            [Environment]::NewLine + [Environment]::NewLine +
            'podman compose ps:' + [Environment]::NewLine +
            $composeStatus + [Environment]::NewLine +
            'Inspect SQL Server logs with: ' +
            'podman compose logs sqlserver'
        )
    }

    Invoke-Podman @(
        'exec', 'crankdemo-sqlserver',
        '/opt/mssql-tools18/bin/sqlcmd', '-S', 'localhost',
        '-U', $settings.UserName, '-P', $settings.Password, '-C', '-b',
        '-d', 'master', '-i', '/seed/001-recreate-database.sql'
    )

    & (Join-Path $PSScriptRoot 'Invoke-Migrations.ps1')

    # Verify that the migration-only command created schema but inserted no
    # Identity demo user before the explicit seed operation runs.
    $schemaOnlyCheck =
        'IF EXISTS (SELECT 1 FROM dbo.AspNetUsers) ' +
        "THROW 51000, 'Migration inserted demo data.', 1;"
    Invoke-Podman @(
        'exec', 'crankdemo-sqlserver',
        '/opt/mssql-tools18/bin/sqlcmd', '-S', 'localhost',
        '-U', $settings.UserName, '-P', $settings.Password,
        '-C', '-b', '-d', $settings.Database,
        '-Q', $schemaOnlyCheck
    )

    & (Join-Path $PSScriptRoot 'Seed-IntegrationDatabase.ps1')

    # The second pass proves the SQL seed remains idempotent.
    & (Join-Path $PSScriptRoot 'Seed-IntegrationDatabase.ps1')
}
finally {
    Pop-Location
}
