# build.ps1
# ----------------------------------------------------------------------------
# Compiles the whole app. Run this to check your code still builds after edits.
#
# HOW TO RUN (from the CCubev2 folder, in PowerShell):
#     .\scripts\build.ps1
# ----------------------------------------------------------------------------

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot   # the CCubev2 folder (one up from scripts/)

Write-Host "Building C-Cube..." -ForegroundColor Cyan
dotnet build "$root\CCube.slnx" --nologo

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild OK." -ForegroundColor Green
} else {
    Write-Host "`nBuild FAILED - read the red text above to see which file and line." -ForegroundColor Red
}
