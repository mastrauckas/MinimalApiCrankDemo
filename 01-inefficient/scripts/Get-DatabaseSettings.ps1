[CmdletBinding()]
param()

$requiredNames = @(
    'MSSQL_SA_PASSWORD',
    'SQLSERVER_HOST',
    'SQLSERVER_PORT',
    'SQLSERVER_DATABASE',
    'SQLSERVER_USER'
)
$values = @{}
foreach ($name in $requiredNames) {
    $value = [Environment]::GetEnvironmentVariable(
        $name,
        [EnvironmentVariableTarget]::Process)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Process environment must define $name."
    }

    $values[$name] = $value
}

[pscustomobject]@{
    Password = $values.MSSQL_SA_PASSWORD
    HostName = $values.SQLSERVER_HOST
    Port = $values.SQLSERVER_PORT
    Database = $values.SQLSERVER_DATABASE
    UserName = $values.SQLSERVER_USER
}
