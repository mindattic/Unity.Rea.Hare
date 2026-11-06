<#
.SYNOPSIS
    Builds the standalone Windows player (H.O.P. High Orbit Patrol) to Build/Hare/Hare.exe.
.DESCRIPTION
    Runs tools/unity.ps1 with HareBuild.BuildWindows (Unity batch mode; the Editor must be closed). The build
    carries the product name, icon and window defaults ProjectSetup.ApplyReleaseSettings writes (U.HARE-T71), and
    tools/play.ps1 launches it. The Unity log goes to Logs/deploy.log.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'Build/Hare/Hare.exe'
if (Test-Path $exe) { Remove-Item $exe -Force }

& (Join-Path $PSScriptRoot 'unity.ps1') -Method Unity.Rea.Hare.EditorTools.HareBuild.BuildWindows -Log deploy
$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) { exit $exitCode }
if (-not (Test-Path $exe)) {
    Write-Error "Unity exited 0 but $exe was not produced; see Logs/deploy.log"
    exit 1
}
Write-Host "Built $exe"
exit 0
