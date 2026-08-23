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

    $ready = $false
    for ($attempt = 1; $attempt -le 60; $attempt++) {
        & podman exec crankdemo-sqlserver `
            /opt/mssql-tools18/bin/sqlcmd -S localhost `
            -U $settings.UserName `
            -P $settings.Password -C -Q 'SELECT 1' *> $null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }

        Start-Sleep -Seconds 2
    }

    if (-not $ready) {
        throw 'SQL Server was not reachable within 120 seconds.'
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
