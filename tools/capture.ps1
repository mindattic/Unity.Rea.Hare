<#
.SYNOPSIS
    U.HARE-T70: runs Unity.Hare's scripted full run and stores a screenshot of every room and mode.
.DESCRIPTION
    Builds the capture-enabled player (MindAttic.Capture's CaptureBuild.BuildWindows, to Builds/Capture) unless
    -NoBuild or one is already there, launches it with -demo (Unity.Hare.Demo.ScriptedRun: startup, title, story,
    map, intro, a level, a kill, the level results, the side scroll, the shaft and the garage) and
    -capture screen, muted, and waits for it. The images land in R:\MindAttic\Capture\<repo>\ (MindAttic.Capture's
    output folder); this script lists the ones this run wrote. Exit code is the player's: non-zero when a room never
    came up, a kill did not score or a shot failed. Needs ffmpeg on PATH, as MindAttic.Capture does.
.PARAMETER NoBuild
    Skip building even when no capture player exists (fails then).
.PARAMETER Rebuild
    Build the capture player even when one exists.
.PARAMETER Mode
    The -capture mode (default 'screen'; 'video' films the whole run).
#>
param(
    [switch]$NoBuild,
    [switch]$Rebuild,
    [string]$Mode = 'screen'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$captureDir = Join-Path $root 'Builds/Capture'
$log = Join-Path $root 'Logs/capture.log'

function Find-Player {
    Get-ChildItem $captureDir -Filter *.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notmatch 'Crash' } | Select-Object -First 1
}

$player = Find-Player
if ($Rebuild -or (-not $player -and -not $NoBuild)) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'unity.ps1') `
        -Method MindAttic.Capture.Unity.Editor.CaptureBuild.BuildWindows -Log capture-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $player = Find-Player
}
if (-not $player) { throw 'No capture player in Builds/Capture; run without -NoBuild.' }

New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
$started = Get-Date
$previous = $env:MINDATTIC_CLI_MUTED
$env:MINDATTIC_CLI_MUTED = '1'
try {
    $arguments = @('-logFile', "`"$log`"", '-muted', '-demo', '-capture') + $Mode.Split(' ')
    $process = Start-Process -FilePath $player.FullName -ArgumentList $arguments -PassThru
    $null = $process.Handle
    $process.WaitForExit()
} finally {
    if ($null -eq $previous) { Remove-Item Env:MINDATTIC_CLI_MUTED -ErrorAction SilentlyContinue } else { $env:MINDATTIC_CLI_MUTED = $previous }
}

$out = 'R:\MindAttic\Capture'
if (Test-Path $out) {
    Get-ChildItem $out -Recurse -File | Where-Object { $_.LastWriteTime -ge $started } |
        ForEach-Object { Write-Host $_.FullName }
}
if (Test-Path $log) { Select-String -Path $log -Pattern '\[Unity\.Hare\]' | ForEach-Object { $_.Line } }
Write-Host "exit=$($process.ExitCode)  log=$log"
exit $process.ExitCode
