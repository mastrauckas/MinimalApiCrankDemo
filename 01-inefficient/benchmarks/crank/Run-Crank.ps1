[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $BearerToken,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $ResultPath
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

if ([string]::IsNullOrWhiteSpace($BearerToken) -and
    [string]::IsNullOrWhiteSpace($ResultPath)) {
    Write-Warning (
        'benchmarks/crank/Run-Crank.ps1 is deprecated as an entry point. Use ' +
        'scripts/Invoke-BenchmarkSetupAndCrank.ps1 instead.')
    & (Join-Path $projectRoot `
        'scripts/Invoke-BenchmarkSetupAndCrank.ps1')
    return
}

if ([string]::IsNullOrWhiteSpace($BearerToken) -or
    [string]::IsNullOrWhiteSpace($ResultPath)) {
    throw 'BearerToken and ResultPath must be supplied together.'
}

$resolvedResultPath = [IO.Path]::GetFullPath($ResultPath)
$resultDirectory = Split-Path -Parent $resolvedResultPath
$agentProcess = $null

function Install-OrUpdateTool {
    param(
        [Parameter(Mandatory)][string] $Package,
        [Parameter(Mandatory)][string] $Command
    )

    $installed = dotnet tool list --global |
        Select-String -SimpleMatch $Package
    if ($installed) {
        dotnet tool update $Package --global --version '0.2.0-*'
    }
    else {
        dotnet tool install $Package --global --version '0.2.0-*'
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install or update $Command."
    }
}

New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
Install-OrUpdateTool 'Microsoft.Crank.Controller' 'crank'
Install-OrUpdateTool 'Microsoft.Crank.Agent' 'crank-agent'

try {
    try {
        Invoke-WebRequest -Uri 'http://127.0.0.1:5010' `
            -TimeoutSec 2 | Out-Null
    }
    catch {
        $agentProcess = Start-Process -FilePath 'crank-agent' `
            -ArgumentList '--url', 'http://localhost:5010' `
            -WindowStyle Hidden -PassThru

        $agentReady = $false
        for ($attempt = 1; $attempt -le 30; $attempt++) {
            try {
                Invoke-WebRequest -Uri 'http://127.0.0.1:5010' `
                    -TimeoutSec 2 | Out-Null
                $agentReady = $true
                break
            }
            catch {
                Start-Sleep -Seconds 1
            }
        }
        if (-not $agentReady) {
            throw 'The local Crank agent was not ready within 30 seconds.'
        }
    }

    $crankExitCode = 0
    try {
        crank --config (Join-Path $PSScriptRoot 'crank.yml') `
            --scenario products --profile local `
            --variable "bearerToken=$BearerToken" `
            --no-metadata `
            --json $resolvedResultPath
        $crankExitCode = $LASTEXITCODE
    }
    finally {
        # Crank includes job variables and raw request headers in its JSON.
        # Remove the short-lived Identity token before retaining the result.
        if (Test-Path -LiteralPath $resolvedResultPath) {
            $resultJson = [IO.File]::ReadAllText($resolvedResultPath)
            $redactedJson = $resultJson.Replace(
                $BearerToken,
                '[REDACTED]')
            [IO.File]::WriteAllText(
                $resolvedResultPath,
                $redactedJson,
                [Text.UTF8Encoding]::new($false))
        }
    }
    if ($crankExitCode -ne 0) {
        throw "Crank failed with exit code $crankExitCode."
    }
}
finally {
    if ($agentProcess) {
        Stop-Process -Id $agentProcess.Id -ErrorAction SilentlyContinue
    }
}

Write-Host "Crank results: $resolvedResultPath"
