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
$StartupAttempts = 60
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
    param([string]$Environment = 'Development', [string]$ConnectionString = $null)

    if (Test-Path $PidFile) {
        throw "A PID file already exists at $PidFile. Run with -Stop first."
    }
    Import-DotEnv

    $childEnvironment = @{ ASPNETCORE_ENVIRONMENT = $Environment }
    if ($ConnectionString) { $childEnvironment['RECALLRADAR_CONNECTION'] = $ConnectionString }

    $arguments = @('run', '--project', $ApiProject, '--no-launch-profile', '--urls', "http://127.0.0.1:$Port")
    $process = Start-Process -FilePath 'dotnet' -ArgumentList $arguments `
        -WorkingDirectory $RepoRoot -PassThru `
        -Environment $childEnvironment
    $process.Id | Out-File -FilePath $PidFile -Encoding ascii -NoNewline
    Write-Host "Started process $($process.Id) on http://127.0.0.1:$Port ($Environment)"
    return $process.Id
}

function Build-WebClient {
    <#
    .SYNOPSIS
    Builds the web client into the API's wwwroot so the browser suite loads the real page.
    #>
    Push-Location (Join-Path $RepoRoot 'web')
    try {
        if (-not (Test-Path 'node_modules')) { npm ci }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "The web client build failed with exit code $LASTEXITCODE." }
    }
    finally {
        Pop-Location
    }
}

function New-FixtureDatabase {
    <#
    .SYNOPSIS
    Creates an empty throwaway database for the browser suite and returns its connection string.

    .DESCRIPTION
    A separate database, dropped and recreated each run, so the suite always starts from the same
    fixture and a developer's own loaded records are never touched by running the tests.
    #>
    Import-DotEnv
    $developerConnection = [Environment]::GetEnvironmentVariable('RECALLRADAR_CONNECTION')
    if (-not $developerConnection) { throw 'RECALLRADAR_CONNECTION is not set. Copy .env.example to .env.' }

    $fixtureSettings = @{}
    foreach ($pair in $developerConnection.Split(';')) {
        if (-not $pair) { continue }
        $separator = $pair.IndexOf('=')
        if ($separator -lt 1) { continue }
        $fixtureSettings[$pair.Substring(0, $separator).Trim()] = $pair.Substring($separator + 1).Trim()
    }

    $sourceDatabase = $fixtureSettings['Database']
    $fixtureDatabase = "$($sourceDatabase)_uxfixture"
    $user = $fixtureSettings['Username']

    docker exec recall-radar-postgres psql -U $user -d $sourceDatabase -q `
        -c "DROP DATABASE IF EXISTS $fixtureDatabase" -c "CREATE DATABASE $fixtureDatabase" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not recreate the fixture database. Is docker compose up?' }

    $fixtureSettings['Database'] = $fixtureDatabase
    return (($fixtureSettings.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ';')
}

function Wait-ForApp {
    <#
    .SYNOPSIS
    Waits until the application answers, rather than assuming a fixed delay is long enough.
    #>
    for ($attempt = 1; $attempt -le $StartupAttempts; $attempt++) {
        try {
            $health = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/health" -TimeoutSec 5
            if ($health.database -eq 'ok') {
                Write-Host "Application is ready (answering: $($health.answering))."
                return
            }
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }

    throw "The application did not become ready within $StartupAttempts seconds."
}

function Invoke-CypressSuite {
    <#
    .SYNOPSIS
    Runs the Cypress suite against a freshly seeded application, then stops it.

    .DESCRIPTION
    The fixture matters as much as the run. The support file refuses to start against an empty
    database, so a green run proves the interface works on records that are actually there.
    #>
    Stop-RunningApp
    Build-WebClient
    $fixtureConnection = New-FixtureDatabase
    $startedPid = Start-App -Environment 'UxFixture' -ConnectionString $fixtureConnection
    try {
        Wait-ForApp
        Push-Location (Join-Path $RepoRoot 'tests\ux')
        try {
            if (-not (Test-Path 'node_modules')) { npm ci }
            npx cypress install | Out-Null
            # Chrome, not Cypress's bundled Electron. Electron 118 crashes with an access violation
            # on real keystrokes into an input bound to a <datalist>, and it is not what anyone uses.
            npx cypress run --browser chrome
            $exitCode = $LASTEXITCODE
        }
        finally {
            Pop-Location
        }
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
