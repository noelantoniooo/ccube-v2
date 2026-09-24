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
  types/scans a barcode by hand; it runs the *exact same* validate → display → save path
  as a PLC case (through `ScanEngine.ManualCheck` → the same `Report()`), only it writes
  nothing back to the PLC.
- The run screen is a **light/dark industrial HMI** (see `Forms/Theme.cs`, toggle in the
  header). It opens maximized and **cannot be closed from the UI** (no minimize/maximize/
  close buttons, Alt+F4 blocked — only Task Manager can end it). It auto-scans (no start/
  stop button) and shows a **Product Information** card (labels for the last/current scan),
  a full-width **Case Logs** table, **Event Logs**, and a PLC/InExpress/Scanner **health
  strip** (green only when confirmed good, red otherwise). The **Manual Scan** bar toggles
  in at the *top* of the Case Logs area (not the window bottom, where a maximized window's
  taskbar hides it). The ML+ Solutions logo (`CCube/Assets/ml-logo.png`) sits top-left.
  Case Logs has 9 columns: Id (from InExpress), Station, Barcode, Case Packer # (parsed
  from the barcode), Product Description + FA Code (from InExpress), Production Date/Time
  (parsed from the barcode), Validation Result (Good/Reject), Scanned Date/Time.

## Where things live (single project, `CCube/`)

| Folder | What's in it |
|---|---|
| `Program.cs` | App start-up: single-instance guard, opens `MainForm`. |
| `Forms/` | The screens: `MainForm` (operator run screen), `SettingsForm`, `AdminPromptForm`, `Theme`. |
| `Plc/` | The PLC contract + link. `Db100Layout`/`Db100Frame`/`S7Data` = the byte format; `IPlcLink` with `S7PlcLink` (real) and `DemoPlcLink` (fake); `ResultCode` (verdict reasons). |
| `Core/` | The logic: `ScanEngine` (the loop), `Validation`, `HealthMonitor`, `Settings*`, `CredentialProtector`, `PasswordHasher`, `AppLog`. |
| `Data/` | SQL seams: `IValidator`+`InExpressValidator` (the validate SP call → `CaseResult`), `IHistorySink` with `SqlHistoryRepository` (INSERT ScanHistory), `CsvHistoryRepository` (daily CSV backup) and `CompositeHistorySink` (writes to both at once). |
| `Models/` | Small data holders like `ScanRecord`. |
| `database/` | SQL scripts: CCube schema + the InExpress validate proc (reference stub). |
| `scripts/` | `build.ps1`, `start.ps1`, `publish.ps1`, `watch-log.ps1`. |
| `docs/` | `DESIGN.md` (living spec) and `HOW-TO-BUILD-RUN-DEBUG.md`. |

## The scan loop (the heart of the app)

`Core/ScanEngine` polls the PLC ~every 200 ms:
1. Read the whole PLC data block once (a consistent snapshot).
2. New case? (the PLC's `SeqNo` changed **and** its heartbeat is alive).
3. Scanner no-read or implausible barcode → REJECT immediately (no DB call).
4. Otherwise call `InExpressValidator` → the validate proc returns one row:
   `Id, ProductDescription, FaCode, IsValid`. `IsValid=1` → PASS, `0` → REJECT. Hard ~2 s
   timeout; any error/timeout ⇒ REJECT (fail-safe).
5. Write the result to the PLC, echoing `AckSeqNo` **last** so the PLC knows the answer is ready.
6. Build the `ScanRecord`: the InExpress fields above **plus** two fields parsed from the
   barcode client-side for display only — **Case Packer #** and **Production Date/Time**
   (`Core/Validation.cs` → `BarcodeFields`, v1's CBL positions). Parsing is display/history
   only; it is NEVER part of the verdict.
7. Save the scan to history — to **both** the CCube DB and a daily **CSV** backup (the boss
   wants two copies), off the critical path so neither can stall the line.

Two heartbeats run continuously (~500 ms): watch the **PLC heartbeat** (detect a hung PLC) and
pulse our own **PC heartbeat** (so the PLC fail-safe-rejects if we die).

**Machine # masking** (Configuration → checkbox + a grid of machine #s 11-46, stored in
`settings.json` under `Masking`). When a scanned case's **Case Packer # (machine)** is ticked, the
case is still validated, saved to CCube + CSV, and shown with its **real** result — the *only*
change is the signal sent back to the PLC in step 5 is forced to **GOOD**, so the case is
physically kept (never ejected) whatever InExpress said. This is a deliberate operator override,
so it doesn't violate the fail-safe rule (it's an explicit config, not silence). It's checked in
`ScanEngine.IsMachineMasked` and applied only on the PLC path (manual scans don't touch the PLC).

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

- **InExpress stored proc** is not final. PMI's contract: `usp_ValidateCase(@Barcode)` returns
  ONE row `Id INT, ProductDescription NVARCHAR, FaCode NVARCHAR, IsValid BIT`. This is isolated
  in `Data/InExpressValidator.cs`; the local dummy (`database/02-inexpress-validate.sql`, a seeded
  `dbo.CaseData` table) simulates it. Confirm the final name/params/columns with PMI.
- **PLC data-block number + byte offsets** live in `Plc/Db100Layout.cs`; confirm against the real
  TIA Portal program. The DB must have "optimized block access" **off** and PUT/GET enabled.
- **Connection strings / PLC IP / station id** are configured per-machine in `settings.json` — no
  code change to move from laptop to plant.
- **Barcode field positions** (v1's CBL rule, all in `Core/Validation.cs` → `BarcodeFields`,
  1-based chars): **Case Packer #** = chars 33-34; **Production Date/Time** = year 23-24,
  month 25-26, day 27-28, hour 35-36, min 37-38, sec 39-40. These feed display/history only,
  never the verdict. Confirm the real positions with PMI; they return null for a short/garbled
  barcode so the grid shows a blank cell instead of a wrong value.
- **Local test databases**: `localhost\SQLEXPRESS`, `InExpress2` (dummy `dbo.CaseData` + the
  dummy `usp_ValidateCase`) and `CCube2` (`dbo.ScanHistory`). Recreate/refresh them by running
  `database/02-inexpress-validate.sql` and `database/01-ccube-schema.sql`.
- **CSV history backup** is written to `AppSettings.HistoryCsvFolder` (default `C:\CCube\History`),
  one file per day under a `yyyy-MM` subfolder. It runs alongside the DB save, not instead of it.

## Reference material (not shipping code)

- `C:\Users\Asus\Projects\CCubev1` — the original VB app (business-logic source of truth).
- `C:\Users\Asus\Projects\__backup\c-cube` — a mature WPF rebuild we borrow PLC/security patterns from.
- `C:\Users\Asus\Projects\__backup\CCubev2` — an earlier simple WinForms attempt.
