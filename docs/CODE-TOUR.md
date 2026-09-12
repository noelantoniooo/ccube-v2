# C-Cube v2 — Code Tour & Developer Guide

A plain-English walkthrough of the codebase for whoever maintains it (that's you).
Read this once end-to-end, then keep it open as a map when you need to change or
debug something. It answers three questions:

0. **The C# fundamentals** you keep seeing in this project (§0 — read first if
   you're new to C#).
1. **What does each file do**, in layman's terms?
2. **How do the files connect** to each other?
3. **When we switch from the local test databases to the real plant databases,
   what do I touch** — and is it "just the stored procedure" or are there queries
   in the code too?

> New here? Also read `HOW-TO-BUILD-RUN-DEBUG.md` (how to compile/run) and
> `DESIGN.md` (the big-picture spec). This file is the "tour of the rooms".

---

## 0. C# fundamentals you'll see all over this project

If you come from PHP/JavaScript, this section decodes the keywords and symbols that
appear on almost every line. You don't need to memorise them — skim once, then come
back when a symbol puzzles you.

### Method (function) shape — the thing you already spotted

In C# the **return type comes first**, then the name, then the parameters. There's
no `function` keyword.

```csharp
private ResultCode DecideVerdict(string barcode)
//  │       │           │            └─ parameters (type name, like `string barcode`)
//  │       │           └─ method name
//  │       └─ RETURN TYPE — what the method hands back
//  └─ access — who is allowed to call it
```

PHP writes `public function decide($barcode): ResultCode`. C# just moves the return
type to the front. `void` as the return type means "returns nothing".

### Access keywords — who can see/call this

| Keyword | Means |
|---|---|
| `public` | Anyone, any file, even outside the project. |
| `private` | Only inside *this same class*. Most fields/methods are private. |
| `internal` | Anywhere in *this project*, but not outside it. |
| `protected` | This class + classes that inherit from it (rare here — we use `sealed`). |

### The keywords that decorate classes, fields and methods

- **`sealed`** — "nobody can inherit from this class." `public sealed class ScanEngine`
  means you can't make a `class MyScanEngine : ScanEngine`. Read it as **"final,
  use as-is."** It's a clarity/safety signal (and a tiny speed win). Harmless to you.
- **`static`** — belongs to the *class itself*, not to an object you `new` up. That's
  why `AppLog.Info(...)` works with no `new AppLog()` — you call it
  `ClassName.Method()`. `Program.Main` is static because the app needs an entry
  point before any object exists.
- **`readonly`** — assigned once (in the constructor) then frozen. `private readonly
  IPlcLink _plc;` guarantees `_plc` never gets swapped out later.
- **`const`** — a fixed value baked in at compile time, e.g. `private const string
  InsertSql = "...";`.
- **`void`** — return type meaning "hands nothing back" (`public void Start()`).
- **`volatile`** — "this field is touched by more than one thread, always read the
  freshest value." You see it on `_running` because the background loop and the UI
  both read it.

### Symbols that trip people up

- **`_underscore` names** (`_plc`, `_running`) — just a **naming convention** for a
  private field of the class (not a keyword). It visually separates fields from
  local variables and parameters.
- **`?` after a type** — "this can be **null** (missing/empty)." `string? ProductName`
  may be null; plain `string` is expected to always have a value.
- **`?.` (null-safe dot)** — `info?.ProductName`: if `info` is null, the whole
  expression is null instead of crashing. Pairs with `??` ("use this if null"):
  `record.ProductName ?? DBNull.Value`.
- **`=>` has two uses:**
  - a **lambda** (a mini inline function): `() => _history.Save(record)` = "a
    function that takes no args and saves the record."
  - an **expression body** (one-line method): `public void Dispose() => Stop();` is
    just a short way to write a method whose whole body is one line.
- **`new`** — build an instance by running its constructor: `new ScanEngine(...)`.
- **`using var x = ...`** — **auto-cleanup**. `using var connection = new
  SqlConnection(...)` automatically closes/disposes the connection when the block
  ends, even if an error is thrown. Prevents leaked DB connections.
- **`$"...{value}..."`** — string interpolation (like PHP double-quoted strings):
  `$"seq {inputs.SeqNo}"` drops the value straight in. `@"..."` is a raw string
  (backslashes and line breaks kept literally) — used for the multi-line SQL.

### The building blocks that make the app modular

- **`interface` and the `I` prefix** — a **contract**: method names with no bodies.
  `IPlcLink` says "any PLC link must have `ReadInputs()`, `WriteOutputs()`…". A real
  class *fulfils* it: `class S7PlcLink : IPlcLink`. The `:` means "implements" (or
  "inherits"). This is the trick that lets `ScanEngine` talk to *any* PLC link
  without knowing which one — it only depends on the interface.
- **`record`** — a lightweight class for holding data (like `ScanRecord`,
  `CaseResult`). C# writes the boilerplate (constructor, equality) for you. Think
  "a data sheet."
- **`enum`** — a fixed set of named options. `ResultCode` (Ok, NotValid, Timeout…)
  is an enum, so the compiler stops you inventing a verdict that doesn't exist.
- **`event` + `+=`** — a **broadcast** others subscribe to. `ScanEngine` *raises*
  `ScanCompleted`; `MainForm` *subscribes* with `_engine.ScanCompleted +=
  OnScanCompleted` ("when a scan finishes, run my method"). Publish/subscribe — it's
  how the background engine tells the screen to update without knowing about the
  screen.

### Threads and "async" (why the conveyor never freezes)

- **`Task.Run(() => ...)`** — "do this on a **background thread**, don't wait for
  it." History-saving uses this so a slow database can't delay the next case.
- **`async` / `await` / `Task`** — the general pattern for "start slow work, let the
  app stay responsive, continue when it's done." You'll see `async` methods return
  a `Task`.
- **`BeginInvoke(...)`** — "run this **on the UI thread**." The scan engine runs on
  a background thread and is *not allowed* to touch screen controls directly, so it
  hops back onto the UI thread through `BeginInvoke` to update labels/lists.

### Predefined C# / WinForms calls you'll see (and can mostly leave alone)

| Call | What it does |
|---|---|
| `InitializeComponent()` | Auto-generated in `*.Designer.cs`. Creates every button/label/panel and sets positions, sizes, colours. The visual designer writes it; you rarely hand-edit it. Called once in the form's constructor to "build the window." |
| `ApplicationConfiguration.Initialize()` | Turns on modern visual styles + high-DPI so the UI looks crisp (in `Program.cs`). |
| `Application.Run(new MainForm())` | Starts the WinForms **message loop** — keeps the window alive and listening for clicks until it closes. |
| `MessageBox.Show(...)` | The little popup dialog box. |
| `Mutex(...)` | A machine-wide flag; we use it so only one copy of C-Cube can run at once. |
| `Environment.TickCount64` | A millisecond clock we use to measure "how long did this take" and heartbeat timing. |
| `Thread.Sleep(200)` | Pause this thread 200 ms — the scan loop's pacing. |
| `Dock` / `Anchor` / `Padding` / `Margin` | WinForms layout: `Dock` glues a control to a side (or fills), `Anchor` keeps edges a fixed distance as the window resizes, `Padding` is space *inside* a control, `Margin` is space *around* it. |

If you remember nothing else: **return type first, `I`-prefix means a contract,
`sealed` means final, `_name` is a private field, `=>` is a mini-function or a
one-line method, and `Task.Run`/`BeginInvoke` are the "don't freeze the conveyor /
don't freeze the screen" tools.**

---

## 1. The 60-second mental model

C-Cube is a referee standing next to a conveyor. A box (a "case") arrives, a
scanner reads its barcode, and the PLC (the factory controller) taps C-Cube on the
shoulder: *"new box — here's its barcode, is it good?"* C-Cube asks the plant
database (**InExpress**), gets back **yes/no**, tells the PLC **PASS or REJECT**
(the PLC physically keeps or ejects the box), and writes a line in its own diary
(**CCube** database) so there's a history.

The single most important rule, baked in everywhere:

> **Silence is never a PASS.** A box passes *only* on an explicit "valid". Any
> timeout, error, no-answer, no-read, or garbage barcode becomes a **REJECT** with
> a named reason. When unsure, reject.

Everything below is just the plumbing that makes that sentence happen reliably and
without ever stalling the conveyor.

---

## 2. The folder map (where things live)

Think of the project as one building (`CCube/`) with a few rooms:

```
CCube/
  Program.cs      → the front door. Starts the app, opens the main window.
  Forms/          → the SCREENS you see (windows, buttons, labels).
  Core/           → the BRAINS (the loop, decisions, settings, security, logging).
  Plc/            → the TRANSLATOR that talks to the Siemens PLC (bytes on a wire).
  Data/           → the DATABASE clerks (ask InExpress; write to CCube history).
  Models/         → tiny DATA SHEETS passed around (e.g. "one scan result").
database/         → SQL scripts to create the tables / the reference stored proc.
scripts/          → one-click PowerShell helpers (build, run, publish, watch logs).
docs/             → this guide and the others.
```

A useful way to hold it in your head:

**Screens (Forms)** show things and take clicks → they hand off to the **Brains
(Core)** → the brains use the **Translator (Plc)** to hear the PLC and the
**Clerks (Data)** to talk to the databases → results travel back as **Data sheets
(Models)** to be shown on the **Screens** again. Round and round, ~5 times a
second.

---

## 3. The important files, one by one

Files are ordered by how much of the real work they do. The heavy hitters are
first (`ScanEngine`, `MainForm`), then the supporting cast.

### ⭐ `Core/ScanEngine.cs` — the heartbeat of the whole app

**What it is:** the background loop. This is *the* file. If you understand this one
file you understand C-Cube. It runs on its own thread (so the screen never
freezes) and wakes up every **200 ms** to do one "tick".

**What one tick does** (read `Tick()` top-to-bottom, that's the whole story):

1. **Keep the InExpress light honest.** Every 5 s it pings InExpress (`SELECT 1`)
   so the health LED reflects reality even when no boxes are arriving.
2. **Is the PLC connected?** If not, it kicks off a reconnect *in the background*
   (so a missing PLC never freezes the loop), sets PLC + Scanner lights red, and
   bails out of this tick. *This is the fail-safe: no PLC ⇒ nothing passes.*
3. **Is the PLC alive?** The PLC has a "heartbeat" number it keeps changing. If
   that number stops moving for 2 s, the PLC is hung → lights red.
4. **Pulse our own heartbeat back** to the PLC (so if *we* die, the PLC's own
   safety logic rejects everything).
5. **New box?** The PLC bumps a `SeqNo` counter for each new case. If `SeqNo`
   changed (and the PLC is alive), call `ProcessCase()`.

**How it decides PASS/REJECT** (`DecideVerdict()` — the fail-safe funnel):

```
scanner no-read?      → REJECT (ScannerNoRead)   ... never even calls the DB
barcode implausible?  → REJECT (BadBarcode)      ... never even calls the DB
ask InExpress:
    valid   → PASS (Ok)
    invalid → REJECT (NotValid)
    timeout → REJECT (Timeout)                   ... DB too slow
    error   → REJECT (Error)                     ... DB threw
```

Notice every branch except the single "valid" answer is a REJECT. That *is* the
golden rule in code form.

**After deciding** (`ProcessCase` → `Report`): it writes the verdict to the PLC,
echoing `AckSeqNo` **last** (that's the "answer is ready" signal), then fires two
things *off the critical path*:
- `ScanCompleted` event → the screen updates (counters, case list).
- `_history.Save(record)` on a throwaway task → writes to the CCube DB. **This is
  deliberately after the verdict and on another thread so a slow database can
  never delay the conveyor.**

**How to debug it:** almost every step calls `AppLog` (Info/Warn/Error), so the
log file is a play-by-play. If boxes are wrongly rejecting, search the log for the
reason code (`Timeout`, `NotValid`, `ScannerNoRead`…) — the word tells you which
branch of `DecideVerdict` fired. Manual Scan (`ManualCheck`) runs the *exact same*
`DecideVerdict`/`Report` path but writes nothing to the PLC — that's your safest
way to test validation without hardware.

**Connects to:** `IPlcLink` (hear/answer the PLC), `IValidator` (ask InExpress),
`IHistorySink` (save history), `HealthMonitor` (the LEDs), and raises
`ScanCompleted` which `MainForm` listens to.

---

### ⭐ `Forms/MainForm.cs` (+ `MainForm.Designer.cs`) — the operator screen

**What it is:** the big window the operator watches all shift. The `.Designer.cs`
file is the *layout* (auto-generated-style: where each panel/label sits); the
`.cs` file is the *behaviour* (what happens on load, on a scan, on a click).

**The one method to read first is `MainForm_Load`** (around line 101). It's the
**wiring diagram** — where all the pieces are assembled and switched on:

```csharp
IValidator validator = new InExpressValidator(...);   // the InExpress clerk
IHistorySink history  = ...IsConfigured ? new SqlHistoryRepository(...)
                                        : new NullHistorySink();  // no-DB fallback
_plcLink = new S7PlcLink(_settings.Plc);              // the PLC translator
_engine  = new ScanEngine(_plcLink, validator, history, _health, stationId);
_engine.ScanCompleted += OnScanCompleted;             // "when a box is answered…"
_engine.Start();                                       // …turn the loop on
```

This is the **composition point** — the only place that says "the real InExpress,
the real PLC, the real history DB go here". If you ever wanted a fake/offline mode
again, this is the single spot you'd swap implementations.

**Other jobs in this file:** mirrors the app log into the on-screen **Event Logs**;
builds the three clickable **counter tiles** (ALL / SUCCESS / FAILED) and their
pop-up case lists (`BuildCounters`, `MakeCounterTile`, `ShowCases`); the **Manual
Scan** panel; and `OnScanCompleted` which bumps the counters and adds the row to
Case Logs.

**How to debug it:** UI glitches (spacing, colours, a control in the wrong place)
live here or in `MainForm.Designer.cs` + `Theme.cs`. Logic bugs (wrong verdict,
nothing happening on a scan) are almost never here — they're in `ScanEngine`. This
form mostly *displays* what the engine hands it.

---

### `Core/Settings.cs` + `Core/SettingsStore.cs` — the config file

**What they are:** `Settings.cs` defines the *shape* of everything configurable
(InExpress connection, CCube connection, PLC IP/rack/slot/DB number, station id,
admin password hash, theme). `SettingsStore.cs` loads/saves that to
`%ProgramData%\CCube\settings.json`.

**Why it matters for you:** this is how the app moves from your laptop to the plant
**without recompiling**. Change the JSON, restart, done. The `Build...ConnectionString()`
methods here assemble the SQL connection strings (integrated Windows auth vs. SQL
username/password) — the Settings screen writes them, these methods read them.

`IsConfigured` on each DB section is the gate that decides whether the app tries to
use that database at all.

---

### `Data/InExpressValidator.cs` — *the* file that talks to InExpress

**What it is:** the clerk that asks the plant "is this case valid?". It opens a
connection, calls **one stored procedure** (`dbo.usp_ValidateCase`) with the
barcode as a **parameter**, reads back one row, and returns `CaseResult`
(valid + optional ProductName/BatchNo/Reason).

**This is the single most important file for the "switch to the real DB"
question** — see §5. It is intentionally the *only* file in the whole app that
knows the procedure name and shape.

**How to debug it:** it logs the barcode and the VALID/INVALID answer for every
call. If validation looks wrong, this log line vs. what you get running the proc
by hand in SSMS tells you instantly whether the bug is in the DB or the app.

---

### `Data/SqlHistoryRepository.cs` — writes the history diary

**What it is:** one parameterized `INSERT` into `dbo.ScanHistory` per finished
scan. It **never throws** — if the DB hiccups it just logs and moves on, because
losing a history row must never stop the line.

**Note the SQL is hand-written here** (`InsertSql` at the top). The column names in
that INSERT must exist in the `ScanHistory` table. (This is exactly what bit us
once: the local table had older column names — see §5.)

`NullHistorySink` (in `IHistorySink.cs`) is the do-nothing stand-in used when no
CCube database is configured, so the app still runs.

---

### `Plc/` — the translator that speaks Siemens

You rarely touch this folder unless the PLC program's memory layout changes. In
order of usefulness to a newcomer:

- **`IPlcLink.cs`** — the tiny contract: `IsConnected`, `EnsureConnected`,
  `ReadInputs`, `WriteOutputs`. `ScanEngine` only ever knows about *this*, which is
  why it doesn't care whether the PLC is real.
- **`S7PlcLink.cs`** — the real implementation using the **S7.NET plus** library.
  Connects to the S7-1200 by IP/rack/slot and reads/writes one Data Block.
- **`Db100Layout.cs`** — the **byte map**: "SeqNo is at byte 0, Barcode starts at
  byte 2 and is 40 bytes…". If the TIA Portal program's DB layout changes, this is
  the file to update. **PMI integration item.**
- **`Db100Frame.cs`** — reads/writes typed values (a short, a string) over one raw
  byte buffer using the layout above.
- **`S7Data.cs`** — the low-level encode/decode of Siemens number/string formats
  (big-endian ints, S7 strings). Rarely touched.
- **`PlcInputs.cs` / `PcOutputs.cs`** — plain data sheets: what we *read from* the
  PLC vs. what we *write to* it. Split on purpose so the app structurally can't
  overwrite PLC-owned bytes.
- **`ResultCode.cs`** — the verdict reasons (`Ok`, `NotValid`, `ScannerNoRead`,
  `BadBarcode`, `Timeout`, `Error`…). `Ok` is the *only* value that means PASS. The
  helpers `IsPass()` / `IsSystemReject()` are used all over.

---

### `Core/` — the rest of the brains

- **`Validation.cs`** — `BarcodeRules.IsPlausible` (is this even a real barcode
  before we bother the DB?) and barcode field helpers. Cheap sanity checks.
- **`HealthMonitor.cs`** — holds the three health states (PLC / InExpress /
  Scanner) and raises `Changed` so the LEDs on screen repaint. Green only when
  *confirmed* good; red otherwise.
- **`AppLog.cs`** — the logger. Writes a daily file under
  `%LocalAppData%\CCube\logs\ccube-YYYYMMDD.log` **and** raises an `Entry` event
  that `MainForm` shows in the Event Logs panel. **This is your #1 debugging
  tool** — `scripts/watch-log.ps1` tails it live.
- **`CredentialProtector.cs`** — DPAPI encrypt/decrypt so DB passwords in
  `settings.json` are ciphertext (`enc:` prefix), never plain text.
- **`PasswordHasher.cs`** — PBKDF2 hash/verify for the single admin password that
  gates editing Settings. Stored only as a hash.
- **`ConnectionTester.cs`** — the "Test connection" buttons in Settings use this to
  try a DB/PLC connection without saving anything.

### `Forms/` — the rest of the screens

- **`SettingsForm.cs`** — read-only config viewer that becomes editable after the
  admin password. Writes `settings.json`. Also where you change the admin password.
- **`AdminPromptForm.cs`** — the little "enter admin password" dialog.
- **`Theme.cs`** — all colours and fonts, plus the light/dark switch. Change the
  look here, not scattered across forms.

### `Models/ScanRecord.cs`

One finished scan as a data sheet (barcode, verdict, reason, timings). It's what
`ScanEngine` produces, what the screen displays, and what `SqlHistoryRepository`
saves. Handy computed props: `IsPass`, `Verdict` ("PASS"/"REJECT"),
`ScannedAtLocal`.

### `Program.cs`

The front door. Ensures only one copy runs (a machine-wide "mutex" — two apps
answering one PLC would be dangerous), installs crash logging, sets the theme, and
opens `MainForm`. Tiny on purpose.

---

## 4. How a single box flows through the files (follow the ball)

```
PLC bumps SeqNo + fills barcode
        │
   Plc/S7PlcLink.ReadInputs()            ← Translator reads the bytes
        │  (PlcInputs data sheet)
        ▼
   Core/ScanEngine.Tick() sees new SeqNo → ProcessCase()
        │
   Core/ScanEngine.DecideVerdict()
        │   ├─ Core/Validation.IsPlausible()      (cheap reject?)
        │   └─ Data/InExpressValidator.Validate() ← Clerk asks InExpress SP
        │            (CaseResult data sheet)
        ▼
   Core/ScanEngine writes verdict → Plc/S7PlcLink.WriteOutputs()  ← PLC accepts/ejects
        │
        ├─ raises ScanCompleted → Forms/MainForm.OnScanCompleted()  (counters + list)
        └─ Data/SqlHistoryRepository.Save()  (history, off-thread)  ← CCube diary
```

Memorize this diagram and you can place any bug: *wrong physical accept/reject* →
Plc + ScanEngine write side; *wrong yes/no* → InExpressValidator + the SP; *right
verdict but not shown* → MainForm; *right verdict but no history row* →
SqlHistoryRepository + the ScanHistory table.

---

## 5. Switching to the real plant databases — what do I actually change?

Short answer: **you do NOT rewrite queries scattered around the code.** The whole
design funnels every database touch through just a few places.

### There are exactly **two** databases and **three** SQL touch-points

| # | Where | File | What it runs | Owned by |
|---|-------|------|--------------|----------|
| 1 | **InExpress** (validate) | `Data/InExpressValidator.cs` | `EXEC dbo.usp_ValidateCase @Barcode` | **PMI/plant DBA writes the proc** |
| 2 | **InExpress** (health ping) | `Data/InExpressValidator.cs` | `SELECT 1` | trivial, leave it |
| 3 | **CCube** (history insert) | `Data/SqlHistoryRepository.cs` | `INSERT INTO dbo.ScanHistory (...)` | **you own this table** |

That's it. There is no other SQL anywhere in the app.

### So is it "just the stored procedure"?

**For InExpress: essentially yes.** The plant's real "is this case valid?" logic
lives *inside* `usp_ValidateCase` on the InExpress server — that was the boss's
whole point (move the rules into the database). The app doesn't count bundles or
apply rules; it just calls the proc and trusts the answer. When PMI gives you the
real InExpress server, you change **connection details only** (in `settings.json`,
no recompile). You'd touch `InExpressValidator.cs` **only if** the real proc has a
different **name, parameters, or result columns** than our assumed contract:

```
EXEC dbo.usp_ValidateCase @Barcode = '...'
  → returns ONE row with an IsValid bit (1=pass/0=reject),
    optionally ProductName / BatchNo / Reason columns.
```

If PMI's proc matches that shape, **zero code changes** — just point the connection
string at the real server. If it differs (say it's `dbo.CheckCase @Code` returning
a column named `Result`), you edit that *one file* and nothing else. The proc name
is even configurable in settings (`InExpress.ProcName`), so a pure rename needs no
code change at all.

**For CCube history: it's a table you own, not a proc.** The `INSERT` in
`SqlHistoryRepository.cs` must match the columns of the real `ScanHistory` table.
Create the real table from `database/01-ccube-schema.sql` (it's the source of
truth) and the INSERT already matches. If the plant's DBA insists on different
column names, update the `InsertSql` string in that one file to match. *(This is
the bug we hit locally: the test DB had an older table with `BatchNumber` /
`InExpressLatencyMs` / `TotalLatencyMs`; the app inserts `BatchNo` / `InExpressMs`
/ `TotalMs`. Fix = recreate the table from the schema script, which we did.)*

### The step-by-step "go live" checklist

1. **Get the real `usp_ValidateCase`** (or its real name/shape) from PMI. Confirm
   it matches the contract above; if not, adjust `Data/InExpressValidator.cs`
   (and/or set `InExpress.ProcName` in settings for a rename).
2. **Create the CCube `ScanHistory` table** on the real CCube server by running
   `database/01-ccube-schema.sql`. Don't hand-edit an old table — run the script.
3. **Open Settings in the app** (admin password), fill in:
   - InExpress: server, database, auth (Windows or SQL user/pass).
   - CCube: server, database, auth.
   - PLC: IP address, rack, slot, DB number (from TIA Portal — a PMI item).
   - Station id.
4. **Test connection** buttons (they only read, safe) → all green.
5. **Restart**, watch the health LEDs and the log. Manual-scan a known-good and a
   known-bad barcode to confirm PASS/REJECT before trusting the line.

Nothing in steps above needs a recompile except case (1) *if* the proc shape
differs. Everything else is configuration + SQL scripts.

### `database/` folder reference

- **`01-ccube-schema.sql`** — creates `dbo.ScanHistory` (+ index). Idempotent; the
  source of truth for the history table's columns.
- **`02-inexpress-validate.sql`** — a **reference stub** of `usp_ValidateCase` so
  you can run locally. The *real* one lives on the plant's InExpress server and is
  PMI's to write; ours is just a stand-in for testing.

---

## 6. Debugging playbook (where to look when X happens)

| Symptom | Look here first |
|---|---|
| Every box rejects, InExpress LED red | InExpress unreachable/slow. Check `settings.json` conn string; run the proc in SSMS; read the log for `Timeout`/`Error`. |
| Wrong yes/no on a valid box | `usp_ValidateCase` logic (in the DB), then `InExpressValidator.cs`. Compare app log line vs. running the proc by hand. |
| Box answered but PLC doesn't accept/eject | `Plc/S7PlcLink.cs` + `Db100Layout.cs` byte offsets; confirm PLC has optimized-block-access OFF and PUT/GET enabled. |
| Verdict right on screen but no history row | `SqlHistoryRepository.cs` INSERT vs. real `ScanHistory` columns; check log for "Could not save scan…". |
| PLC LED red / "NOT RESPONDING" | PLC heartbeat not advancing — PLC program or network. `ScanEngine.Tick` watchdog. |
| UI looks wrong (spacing/colour/layout) | `MainForm.Designer.cs`, `MainForm.cs` build methods, `Theme.cs`. Not a logic bug. |
| App won't start / crashes on launch | `Program.cs` catch block + the log file (`%LocalAppData%\CCube\logs`). |
| Can't edit Settings | Admin password (`AdminPromptForm` + `PasswordHasher`). |

**Golden debugging habit:** run `scripts/watch-log.ps1` in a terminal while the app
runs. Nearly every meaningful step logs a line; the log is a narration of exactly
what the app decided and why.

---

## 7. One-paragraph summary to remember

`Program.cs` opens `MainForm`, which (in `MainForm_Load`) wires up an
`InExpressValidator` (asks the DB), a `SqlHistoryRepository` (saves history) and an
`S7PlcLink` (talks to the PLC), and starts a `ScanEngine`. The engine loops 5×/sec:
read the PLC, on a new case decide PASS only on an explicit "valid" (else REJECT
with a reason), write the verdict to the PLC, then update the screen and save
history off the critical path. To go live you mostly change **connection settings**
and run the **SQL scripts**; real code changes are confined to `InExpressValidator.cs`
(only if the plant's proc differs) and `SqlHistoryRepository.cs` (only if the
history table's columns differ). There are no other queries hiding in the codebase.
