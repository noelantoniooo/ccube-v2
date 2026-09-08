# C-Cube v2 — Design (living document)

> This is the running spec. We update it as we build. Last updated: 2026-09-03 (Phase 1 scaffold).

## 1. Purpose

C-Cube validates shipping cases (cigarette boxes) on a conveyor. For each case the PLC presents,
the app decides **PASS** (valid — keep the case) or **REJECT** (invalid — eject it), and records
the scan. The app is the "brain" between three things it does **not** own: the barcode scanner, the
Siemens S7-1200 PLC, and the InExpress MES database.

## 2. End-to-end flow

```
 [Scanner] --writes barcode--> [S7-1200 PLC] <==read/write==> [C-Cube app] --SP--> [InExpress DB]
                                                                     |
                                                                     +--log--> [CCube DB]
```

1. A case reaches the station; the scanner reads its barcode and writes it into the PLC.
2. The PLC raises a "new case" signal (increments a sequence number) with the barcode.
3. C-Cube reads the barcode from the PLC.
4. C-Cube calls one InExpress stored procedure with the barcode → gets `1` (valid) or `0` (invalid).
5. C-Cube writes the verdict back to the PLC; the PLC keeps or ejects the case.
6. C-Cube logs the scan (barcode, verdict, reason, timing) to the CCube database.

**Fail-safe:** a case passes only on an explicit `1`. Anything else (no-read, bad barcode, DB
timeout, DB error, unknown) → REJECT with a specific reason code.

## 3. PLC contract (data block)

The app and PLC share one Data Block, read/written as a single block so all fields are one
consistent snapshot. Field names and directions below; **exact DB number and byte offsets are a PMI
integration item** and live in `Plc/Db100Layout.cs`.

| Field | Direction | Meaning |
|---|---|---|
| `SeqNo` | PLC → PC | Bumped (last) when a new case is presented. A change = new case. |
| `Barcode` | PLC → PC | The case barcode (S7 String). |
| `PlcHeartbeat` | PLC → PC | Counts up ~every 500 ms; frozen = PLC hung. |
| `ScannerNoRead` | PLC → PC | The scanner failed to read this case. |
| `PlcReady` | PLC → PC | PLC is in run/ready state. |
| `AckSeqNo` | PC → PLC | Echoes `SeqNo` (written **last**) to say "verdict is ready". |
| `ResultValid` | PC → PLC | This verdict word is meaningful. |
| `ResultGood` | PC → PLC | `true` = PASS, `false` = REJECT. |
| `ResultCode` | PC → PLC | Reason code (see `Plc/ResultCode.cs`). |
| `PcHeartbeat` | PC → PLC | Our liveness pulse; if it stops, the PLC fail-safe-rejects. |
| `PcOnline` | PC → PLC | We're alive **and** able to validate (distinct from heartbeat). |

**Handshake:** PLC writes barcode then bumps `SeqNo` last; PC writes the verdict then sets
`AckSeqNo = SeqNo` last. Each side waits for the other's "last" field before trusting the rest.

**TIA Portal requirements:** on the shared DB, "optimized block access" must be **OFF** (so absolute
byte addressing works) and **PUT/GET** communication enabled. S7.NET plus speaks classic S7comm.

## 4. Validation (InExpress)

- One call per scan: `usp_ValidateCase(@Barcode NVARCHAR(50))` → returns `IsValid BIT` (1/0),
  optionally `ProductName`, `BatchNo`, `Reason` for display/history.
- Parameterized. Hard timeout ~2 s. Timeout or error → system REJECT (never a silent pass).
- **The SP is not final.** All the app assumes lives in `Data/InExpressValidator.cs` + the reference
  script `database/02-inexpress-validate.sql`. When PMI finalizes it, we change those two only.
- Note: v1 computed validity in the app (counting aggregation rows, hardcoded "50 bundles"). In v2
  all that logic moves **into the SP** — the app just asks and obeys.

## 5. CCube database (history/audit)

`dbo.ScanHistory`, one row per case answered:

| Column | Notes |
|---|---|
| `Id` | identity PK |
| `ScannedAtUtc` | when we answered |
| `StationId` | which station (from settings) |
| `Barcode` | the case barcode |
| `Verdict` | `PASS` / `REJECT` |
| `ResultCode` | reason name (e.g. `Ok`, `NotValid`, `ScannerNoRead`, `Timeout`, `Error`) |
| `IsSystemReject` | true when we rejected due to *our* fault (timeout/error), so outages don't hide in the reject rate |
| `ProductName`, `BatchNo` | optional, from the SP |
| `InExpressMs`, `TotalMs` | timings for monitoring |

Full schema in `database/01-ccube-schema.sql` (idempotent — safe to re-run).

## 6. Configuration & security

- Stored in `%ProgramData%\CCube\settings.json`. Sections: InExpress connection, CCube connection,
  PLC (IP/rack/slot/DB number), Station id, Demo, Admin password hash.
- DB passwords stored **DPAPI-encrypted** (`enc:` prefix), tied to this machine.
- Admin password stored only as a **PBKDF2 hash** (never recoverable).
- Settings screen is **read-only**; **Edit** requires the admin password. **Test connection** buttons
  need no password (harmless reads).

## 7. Health checks

- **PLC**: socket connected **and** heartbeat advancing (an open socket alone is not "alive").
- **InExpress**: periodic `SELECT 1` ping, plus live SP-call success/failure.
- **Scanner**: reported *through* the PLC (per-case `ScannerNoRead`, and a scanner-status bit if PMI
  exposes one). The app has no direct scanner link.
- Surfaced as three LEDs on the run screen with plain-language alarm text.

## 8. Demo mode (run with no hardware)

When `Demo.Enabled = true`, the app uses `DemoPlcLink` (an in-memory fake PLC) instead of
`S7PlcLink`. A small panel lets you type a barcode and inject a case through the *real* validate →
result → log path. Turn Demo off at the plant. No mock/test code ships disabled inside the app —
Demo is a first-class, visible mode.

## 9. Open items

- [ ] Final InExpress SP name / params / return shape (PMI).
- [ ] Real PLC DB number + byte offsets + scanner-status bit (PMI / TIA Portal).
- [ ] Confirm CCube DB instance + connection string (email says it's provisioned).
- [ ] Barcode format/length rules (v1 used a fixed 40-char layout; confirm for v2 plausibility check).

## 10. Build order (progress)

- [x] Phase 1 — Scaffold + CLAUDE.md + docs
- [x] Phase 2 — PLC contract + Demo PLC
- [x] Phase 3 — Validation + InExpress (+ logging brought forward)
- [x] Phase 4 — Scan engine + history
- [x] Phase 5 — Run screen (MainForm) — **runnable Demo end-to-end**
- [x] Phase 6 — Settings + admin gate (DPAPI + PBKDF2, read-only→Edit→Save, Test buttons)
- [x] Phase 7 — Real S7 link (`S7PlcLink` via S7.NET plus; used when Demo is off)
- [x] Phase 8 — Polish + docs (config guide, shareable design page)

**Status: feature-complete for Demo and Live. Pending real-hardware validation at the plant.**
