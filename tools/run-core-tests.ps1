# Runs Core's pure-logic tests outside Unity (no editor needed). Extra args pass through to dotnet test.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet test (Join-Path $root 'Tests/Core.Tests') @args
exit $LASTEXITCODE
