<#
.SYNOPSIS
    Builds the standalone Windows player for development to Build/Hare/Hare.exe.
.DESCRIPTION
    Runs tools/unity.ps1 with HareBuild.BuildWindows (Unity batch mode; the Editor must be closed). This is a
    bare dev build -- no icon, product-settings polish or release checklist (U.HARE-T71) -- good enough for
    tools/play.ps1 to launch and prove the game runs, and sounds, as built. The Unity log goes to Logs/deploy.log.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'Build/Hare/Hare.exe'
if (Test-Path $exe) { Remove-Item $exe -Force }

& (Join-Path $PSScriptRoot 'unity.ps1') -Method Unity.Hare.EditorTools.HareBuild.BuildWindows -Log deploy
$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) { exit $exitCode }
if (-not (Test-Path $exe)) {
    Write-Error "Unity exited 0 but $exe was not produced; see Logs/deploy.log"
    exit 1
}
Write-Host "Built $exe"
exit 0
