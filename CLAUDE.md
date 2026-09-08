# CLAUDE.md — C-Cube v2

Guidance for Claude (and any developer) working in this repository. Keep it current.

## What this app is

C-Cube is a **shipping-case (cigarette box) validation station** on a factory conveyor at PMFTC.
A Siemens **S7-1200 PLC** tells the app when a case is in position and hands it the case **barcode**
(the scanner writes into the PLC; the app never talks to the scanner directly). The app asks the
plant **InExpress** MES database "is this case valid?", writes **PASS/REJECT** back to the PLC (which
physically keeps or ejects the case), and logs every scan to its own **CCube** database.

This is a C# rebuild of the original VB.NET app (`C:\Users\Asus\Projects\CCubev1`).

## Golden rules (do not break these)

1. **Fail-safe: silence is never a PASS.** A case passes *only* on an explicit "valid" from
   InExpress. Every timeout, error, missing answer, or unknown state becomes a **REJECT** with a
   named reason code. When in doubt, reject.
2. **Keep it simple and commented.** The owner is not a C# developer. Prefer plain, readable code
   with "what/why" header comments over clever abstractions. No MVVM, no unnecessary layers.
3. **Never block the conveyor.** The verdict must reach the PLC fast. History/DB logging happens
   *after* the verdict is sent and must never delay it.
4. **Never trust input.** All SQL uses parameters (never string concatenation). Barcodes are
   validated for plausibility before use.
5. **Secrets are never plain text.** DB passwords are DPAPI-encrypted in `settings.json`; the admin
   password is stored only as a PBKDF2 hash.

## Tech stack (locked)

- **C# / .NET 10**, **WinForms** (code-behind, no XAML/MVVM), **single project**.
- **S7.NET plus** (`S7netplus`) for the PLC. **Microsoft.Data.SqlClient** for SQL Server.
- Config in **`%ProgramData%\CCube\settings.json`** (secrets DPAPI-encrypted).
- **Manual Scan** is a button on the run screen that opens a panel where the operator
  types/scans a barcode by hand; it validates through InExpress exactly like a PLC case.
- The run screen is a **light/dark industrial HMI** (see `Forms/Theme.cs`, toggle in the
  header); it opens maximized, auto-scans (no start/stop button), and shows Case Logs,
  Event Logs, a PLC/InExpress/Scanner health strip (green only when confirmed good, red
  otherwise), counters, and a reject-reasons breakdown.

## Where things live (single project, `CCube/`)

| Folder | What's in it |
|---|---|
| `Program.cs` | App start-up: single-instance guard, opens `MainForm`. |
| `Forms/` | The screens: `MainForm` (operator run screen), `SettingsForm`, `AdminPromptForm`, `Theme`. |
| `Plc/` | The PLC contract + link. `Db100Layout`/`Db100Frame`/`S7Data` = the byte format; `IPlcLink` with `S7PlcLink` (real) and `DemoPlcLink` (fake); `ResultCode` (verdict reasons). |
| `Core/` | The logic: `ScanEngine` (the loop), `Validation`, `HealthMonitor`, `Settings*`, `CredentialProtector`, `PasswordHasher`, `AppLog`. |
| `Data/` | SQL: `Db` (helper), `InExpressValidator` (the validate SP call), `HistoryRepository` (audit log). |
| `Models/` | Small data holders like `ScanRecord`. |
| `database/` | SQL scripts: CCube schema + the InExpress validate proc (reference stub). |
| `scripts/` | `build.ps1`, `start.ps1`, `publish.ps1`, `watch-log.ps1`. |
| `docs/` | `DESIGN.md` (living spec) and `HOW-TO-BUILD-RUN-DEBUG.md`. |

## The scan loop (the heart of the app)

`Core/ScanEngine` polls the PLC ~every 200 ms:
1. Read the whole PLC data block once (a consistent snapshot).
2. New case? (the PLC's `SeqNo` changed **and** its heartbeat is alive).
3. Scanner no-read or implausible barcode → REJECT immediately (no DB call).
4. Otherwise call `InExpressValidator` → `1` (PASS) or `0` (REJECT), with a hard ~2 s timeout.
5. Write the result to the PLC, echoing `AckSeqNo` **last** so the PLC knows the answer is ready.
6. Log the scan to the CCube DB (off the critical path).

Two heartbeats run continuously (~500 ms): watch the **PLC heartbeat** (detect a hung PLC) and
pulse our own **PC heartbeat** (so the PLC fail-safe-rejects if we die).

## Build / run / test

```powershell
.\scripts\build.ps1      # compile
.\scripts\start.ps1      # build + run
.\scripts\publish.ps1    # make the deployable, self-contained plant build
.\scripts\watch-log.ps1  # watch live logs
```

Or in **Visual Studio 2022**: open `CCube.slnx`, press **F5**. (Visual Studio is recommended for the
WinForms drag-and-drop designer; VS Code works too but has no visual designer.)

## Integration items still open (confirm before plant deployment)

- **InExpress stored proc** is not final. The app calls `usp_ValidateCase(@Barcode) → IsValid bit`
  and isolates that in `Data/InExpressValidator.cs`. Confirm the final name/params/return with PMI.
- **PLC data-block number + byte offsets** live in `Plc/Db100Layout.cs`; confirm against the real
  TIA Portal program. The DB must have "optimized block access" **off** and PUT/GET enabled.
- **Connection strings / PLC IP / station id** are configured per-machine in `settings.json` — no
  code change to move from laptop to plant.
- **Barcode case-packer position** for the Rejects Monitoring grid is taken from characters 33-34 of
  the barcode (v1's rule, in `Core/Validation.cs` → `BarcodeFields.CasePackerId`). Confirm the real
  position with PMI. **Shift boundaries** (1st 06-14, 2nd 14-22, 3rd 22-06) are in `MainForm.ShiftIndex`.

## Reference material (not shipping code)

- `C:\Users\Asus\Projects\CCubev1` — the original VB app (business-logic source of truth).
- `C:\Users\Asus\Projects\__backup\c-cube` — a mature WPF rebuild we borrow PLC/security patterns from.
- `C:\Users\Asus\Projects\__backup\CCubev2` — an earlier simple WinForms attempt.
