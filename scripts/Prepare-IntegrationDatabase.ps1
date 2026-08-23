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
        "MSSQL_SA_PASSWORD=$generatedPassword"
    )
}

$passwordLine = Get-Content -LiteralPath $envPath |
    Where-Object { $_ -match '^MSSQL_SA_PASSWORD=' } |
    Select-Object -First 1
if (-not $passwordLine) {
    throw '.env must define MSSQL_SA_PASSWORD.'
}
$password = $passwordLine.Substring('MSSQL_SA_PASSWORD='.Length)
if ([string]::IsNullOrWhiteSpace($password)) {
    throw 'MSSQL_SA_PASSWORD cannot be empty.'
}

Push-Location $projectRoot
try {
    Invoke-Podman @('compose', 'up', '-d', 'sqlserver')

    $ready = $false
    for ($attempt = 1; $attempt -le 60; $attempt++) {
        & podman exec crankdemo-sqlserver `
            /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa `
            -P $password -C -Q 'SELECT 1' *> $null
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
        '-U', 'sa', '-P', $password, '-C', '-b',
        '-d', 'master', '-i', '/seed/001-recreate-database.sql'
    )

    & (Join-Path $PSScriptRoot 'Apply-Migrations.ps1')

    # Verify that the migration-only command created schema but inserted no
    # Identity demo user before the explicit seed operation runs.
    $schemaOnlyCheck =
        'IF EXISTS (SELECT 1 FROM dbo.AspNetUsers) ' +
        "THROW 51000, 'Migration inserted demo data.', 1;"
    Invoke-Podman @(
        'exec', 'crankdemo-sqlserver',
        '/opt/mssql-tools18/bin/sqlcmd', '-S', 'localhost',
        '-U', 'sa', '-P', $password, '-C', '-b', '-d', 'CrankDemo',
        '-Q', $schemaOnlyCheck
    )
}
finally {
    Pop-Location
}
