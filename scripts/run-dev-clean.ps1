# Launches Recall Radar for development and stops it cleanly afterwards.
#
# Constitution Article II: this script terminates processes ONLY by the process
# id recorded in the PID file it writes itself. It never matches processes by
# name pattern, because a wildcard match could kill the agent's own session or
# the Forge Terminal binary.

[CmdletBinding()]
param(
    [switch]$Stop,
    [switch]$CypressOnly,
    [int]$Port = 5180
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$PidFile = Join-Path $RepoRoot '.recall-radar.pid'
$ApiProject = Join-Path $RepoRoot 'src\RecallRadar.Api'
$StartupGraceSeconds = 4
$DotEnvFile = Join-Path $RepoRoot '.env'

function Import-DotEnv {
    <#
    .SYNOPSIS
    Loads KEY=VALUE lines from the gitignored .env into this process so the app inherits them.

    .DESCRIPTION
    The connection string and any local secrets live only in .env (Article IX). Lines that are
    blank or start with # are ignored. Values already set in the environment are left alone, so
    a vault-injected value always wins over the file.
    #>
    if (-not (Test-Path $DotEnvFile)) {
        throw "No .env file at $DotEnvFile. Copy .env.example to .env and fill in the values."
    }
    foreach ($line in Get-Content $DotEnvFile) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith('#')) { continue }
        $separatorIndex = $trimmed.IndexOf('=')
        if ($separatorIndex -lt 1) { continue }
        $name = $trimmed.Substring(0, $separatorIndex).Trim()
        $value = $trimmed.Substring($separatorIndex + 1).Trim().Trim('"')
        if (-not [Environment]::GetEnvironmentVariable($name)) {
            [Environment]::SetEnvironmentVariable($name, $value)
        }
    }
}

function Stop-RunningApp {
    <#
    .SYNOPSIS
    Stops the application using only the PID recorded in the PID file.
    #>
    if (-not (Test-Path $PidFile)) {
        Write-Host 'No PID file found; nothing to stop.'
        return
    }

    $recordedPid = (Get-Content $PidFile -Raw).Trim()
    if (-not ($recordedPid -match '^\d+$')) {
        Write-Warning 'PID file did not contain a process id. Leaving it alone.'
        return
    }

    $target = Get-Process -Id ([int]$recordedPid) -ErrorAction SilentlyContinue
    if ($null -eq $target) {
        Write-Host "Process $recordedPid is no longer running."
    }
    else {
        Stop-Process -Id ([int]$recordedPid) -Confirm:$false
        Write-Host "Stopped process $recordedPid."
    }
    Remove-Item $PidFile -Force
}

function Start-App {
    <#
    .SYNOPSIS
    Starts the API in the background and records its process id.

    .PARAMETER Environment
    ASP.NET Core environment name. "UxFixture" serves the seeded throwaway
    database the Cypress suite depends on; "Development" uses the compose database.
    #>
    param([string]$Environment = 'Development')

    if (Test-Path $PidFile) {
        throw "A PID file already exists at $PidFile. Run with -Stop first."
    }
    Import-DotEnv

    $arguments = @('run', '--project', $ApiProject, '--no-launch-profile', '--urls', "http://127.0.0.1:$Port")
    $process = Start-Process -FilePath 'dotnet' -ArgumentList $arguments `
        -WorkingDirectory $RepoRoot -PassThru `
        -Environment @{ ASPNETCORE_ENVIRONMENT = $Environment }
    $process.Id | Out-File -FilePath $PidFile -Encoding ascii -NoNewline
    Write-Host "Started process $($process.Id) on http://127.0.0.1:$Port ($Environment)"
    return $process.Id
}

function Invoke-CypressSuite {
    <#
    .SYNOPSIS
    Runs the Cypress suite against a freshly seeded application, then stops it.

    .DESCRIPTION
    The fixture matters as much as the run. The support file refuses to start
    against an empty database, so a green run proves the UI works on real data.
    #>
    Stop-RunningApp
    $startedPid = Start-App -Environment 'UxFixture'
    try {
        Start-Sleep -Seconds $StartupGraceSeconds
        Push-Location (Join-Path $RepoRoot 'tests\ux')
        npx cypress run
        $exitCode = $LASTEXITCODE
        Pop-Location
        if ($exitCode -ne 0) { throw "Cypress failed with exit code $exitCode." }
    }
    finally {
        Stop-Process -Id $startedPid -Confirm:$false -ErrorAction SilentlyContinue
        if (Test-Path $PidFile) { Remove-Item $PidFile -Force }
    }
}

if ($Stop) {
    Stop-RunningApp
}
elseif ($CypressOnly) {
    Invoke-CypressSuite
}
else {
    Start-App | Out-Null
    Start-Sleep -Seconds $StartupGraceSeconds
    Start-Process "http://127.0.0.1:$Port"
    Write-Host 'Recall Radar opened. Stop it with: .\scripts\run-dev-clean.ps1 -Stop'
}
