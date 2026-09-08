# start.ps1
# ----------------------------------------------------------------------------
# Builds the app and then runs it. This is the one you'll use most while
# developing: edit code -> .\scripts\start.ps1 -> see it run.
#
# HOW TO RUN (from the CCubev2 folder):
#     .\scripts\start.ps1
#
# Options:
#     .\scripts\start.ps1 -NoBuild     (just run, skip the build step)
# ----------------------------------------------------------------------------

param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

# Windows locks a running .exe, so close any copy that's already open first.
Get-Process -Name "CCube" -ErrorAction SilentlyContinue | Stop-Process -Force

if (-not $NoBuild) {
    Write-Host "Building..." -ForegroundColor Cyan
    dotnet build "$root\CCube.slnx" --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed - not starting." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Starting C-Cube..." -ForegroundColor Green
dotnet run --project "$root\CCube\CCube.csproj" --no-build
