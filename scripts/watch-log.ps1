# watch-log.ps1
# ----------------------------------------------------------------------------
# Shows the app's log messages live as they happen. Handy for seeing WHY
# something went wrong (a failed PLC connect, a SQL error, etc.).
#
# C-Cube writes a new log file each day to:
#     %LocalAppData%\CCube\logs\ccube-YYYYMMDD.log
#
# HOW TO RUN (from the CCubev2 folder), then leave it open in its own window:
#     .\scripts\watch-log.ps1
# ----------------------------------------------------------------------------

$logDir = Join-Path $env:LOCALAPPDATA "CCube\logs"
$today  = Get-Date -Format "yyyyMMdd"
$logFile = Join-Path $logDir "ccube-$today.log"

Write-Host "Watching $logFile" -ForegroundColor Cyan
Write-Host "(waiting for the app to write its first log line... press Ctrl+C to stop)`n" -ForegroundColor DarkGray

# Wait for the file to exist (the app creates it on first run of the day).
while (-not (Test-Path $logFile)) { Start-Sleep -Milliseconds 500 }

Get-Content $logFile -Wait -Tail 20
