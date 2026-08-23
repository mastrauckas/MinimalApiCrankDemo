[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $EnvPath
)

$values = @{}
Get-Content -LiteralPath $EnvPath | ForEach-Object {
    if ($_ -match '^([^#=]+)=(.*)$') {
        $values[$Matches[1].Trim()] = $Matches[2].Trim()
    }
}

$requiredNames = @(
    'MSSQL_SA_PASSWORD',
    'SQLSERVER_HOST',
    'SQLSERVER_PORT',
    'SQLSERVER_DATABASE',
    'SQLSERVER_USER'
)
foreach ($name in $requiredNames) {
    if ([string]::IsNullOrWhiteSpace($values[$name])) {
        throw ".env must define $name."
    }
}

[pscustomobject]@{
    Password = $values.MSSQL_SA_PASSWORD
    HostName = $values.SQLSERVER_HOST
    Port = $values.SQLSERVER_PORT
    Database = $values.SQLSERVER_DATABASE
    UserName = $values.SQLSERVER_USER
}
