# clear-history.ps1
# ----------------------------------------------------------------------------
# Wipes C-Cube's scan history (dbo.ScanHistory) so you can start testing from a
# clean slate. Optionally also deletes the daily CSV backup files.
#
# WHICH DATABASE: it reads the C-Cube DB server + name from the app's own
# settings file (%ProgramData%\CCube\settings.json), so it clears whatever the
# app is pointed at. Falls back to localhost\SQLEXPRESS / CCube2 if not set.
# It connects with your Windows login (integrated security).
#
# !! At the plant this table is the audit trail - only run it on a TEST database.
#    It always shows you the target and asks you to type YES before deleting.
#
# HOW TO RUN (from the CCubev2 folder):
#     .\scripts\clear-history.ps1                 # clear the DB (asks to confirm)
#     .\scripts\clear-history.ps1 -IncludeCsv     # also delete the CSV backups
#     .\scripts\clear-history.ps1 -Force          # no confirmation prompt
#
# After clearing, restart C-Cube - its Case Logs reload from the DB on startup.
# ----------------------------------------------------------------------------

param(
    [string]$Server,          # override the server from settings.json
    [string]$Database,        # override the database from settings.json
    [switch]$IncludeCsv,      # also delete the daily CSV backup files
    [switch]$Force            # skip the "type YES" confirmation
)

# --- Work out which database to clear (settings.json, unless overridden) ---
$settingsPath = Join-Path $env:ProgramData "CCube\settings.json"
$csvFolder = "C:\CCube\History"
if (Test-Path $settingsPath) {
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
    if (-not $Server)   { $Server   = $settings.CCube.Server }
    if (-not $Database) { $Database = $settings.CCube.Database }
    if ($settings.HistoryCsvFolder) { $csvFolder = $settings.HistoryCsvFolder }
}
if (-not $Server)   { $Server   = "localhost\SQLEXPRESS" }
if (-not $Database) { $Database = "CCube2" }

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Host "sqlcmd was not found. Install the SQL Server command-line tools first." -ForegroundColor Red
    exit 1
}

# --- Show what will be deleted ---
$count = sqlcmd -S $Server -d $Database -E -h -1 -W -b -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.ScanHistory;"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Could not reach $Server / $Database (see the error above)." -ForegroundColor Red
    exit 1
}
$count = "$count".Trim()

Write-Host ""
Write-Host "Target : $Server  /  $Database  (dbo.ScanHistory)" -ForegroundColor Cyan
Write-Host "Rows   : $count will be deleted" -ForegroundColor Cyan
if ($IncludeCsv) {
    Write-Host "CSV    : $csvFolder will be deleted too" -ForegroundColor Cyan
}
Write-Host ""

if (-not $Force) {
    $answer = Read-Host "Type YES to clear the history"
    if ($answer -ne "YES") {
        Write-Host "Cancelled - nothing was deleted." -ForegroundColor Yellow
        exit 0
    }
}

# --- Clear the table (TRUNCATE also resets the Id counter back to 1) ---
sqlcmd -S $Server -d $Database -E -b -Q "TRUNCATE TABLE dbo.ScanHistory;" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to clear dbo.ScanHistory." -ForegroundColor Red
    exit 1
}
Write-Host "Cleared dbo.ScanHistory ($count rows)." -ForegroundColor Green

# --- Optionally delete the CSV backups ---
if ($IncludeCsv) {
    if (Test-Path $csvFolder) {
        Remove-Item $csvFolder -Recurse -Force
        Write-Host "Deleted CSV backups in $csvFolder." -ForegroundColor Green
    } else {
        Write-Host "No CSV backups found at $csvFolder." -ForegroundColor DarkGray
    }
}

Write-Host "Restart C-Cube so its Case Logs start empty." -ForegroundColor DarkGray
