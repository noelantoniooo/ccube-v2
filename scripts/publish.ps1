# publish.ps1
# ----------------------------------------------------------------------------
# Builds a DEPLOYABLE copy of C-Cube for the plant PC.
#
# The output is "self-contained": the .NET runtime is bundled inside, so the
# plant PC needs NOTHING installed - no .NET, no Visual Studio. Just copy the
# whole 'publish' folder across and run CCube.exe.
#
# HOW TO RUN (from the CCubev2 folder):
#     .\scripts\publish.ps1
#
# Options:
#     .\scripts\publish.ps1 -SingleFile   (bundle into one big CCube.exe)
# ----------------------------------------------------------------------------

param(
    [switch]$SingleFile,
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if ($Output -eq "") { $Output = "$root\publish" }

# Close any running copy and wipe the old output so we get a clean package.
Get-Process -Name "CCube" -ErrorAction SilentlyContinue | Stop-Process -Force
if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }

Write-Host "Publishing C-Cube (self-contained, win-x64)..." -ForegroundColor Cyan

$args = @(
    "publish", "$root\CCube\CCube.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:DebugType=none",
    "-o", $Output,
    "--nologo"
)
if ($SingleFile) {
    $args += "-p:PublishSingleFile=true"
    $args += "-p:IncludeNativeLibrariesForSelfExtract=true"
}

dotnet @args

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nDone. Deployable app is in:`n  $Output" -ForegroundColor Green
    Write-Host "Copy that whole folder to the plant PC and run CCube.exe." -ForegroundColor Green
} else {
    Write-Host "`nPublish FAILED." -ForegroundColor Red
}
