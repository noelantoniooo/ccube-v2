# Configuring C-Cube (and going live at the plant)

Everything that changes between your laptop and the plant lives in **settings**, not
in code. You never rebuild to deploy — you just fill in the connections and untick Demo.

## Where settings are stored

`C:\ProgramData\CCube\settings.json`

- DB passwords are **encrypted** (you'll see `enc:...`), tied to that PC.
- The admin password is stored only as a **hash** — it can't be read back.
- You normally edit settings **through the app** (Configuration screen), not the file.

## Opening the Configuration screen

1. Click **⚙ Configuration** (top-right of the run screen).
2. It opens **read-only**. Click **Edit** and enter the admin password.
   - Default admin password: **`Admin#2026`** — change it (see below) before real use.
3. Change what you need, click **Save**, then **restart** when prompted.

### Test connection buttons

Each database and the PLC has a **Test connection** button. These work **without** the
admin password (they only read), so you can check your details before saving.

### Changing the admin password

Configuration → Edit → **Change admin password…** → type it twice → **Save**.

## Settings explained

| Setting | What it is |
|---|---|
| **Station ID** | A short name for this station (stored with every scan). |
| **Manual scan mode** | ON = no PLC hardware; scan barcodes by hand via the Manual Scan panel. OFF = real PLC auto-scans. (Validation uses real InExpress when configured, otherwise a canned demo answer.) |
| **InExpress → Server / Database** | The plant MES SQL Server and database. |
| **InExpress → Use Windows login** | Ticked = connect as the Windows user. Unticked = use the SQL username/password. |
| **InExpress → Validate procedure** | The stored proc to call, default `dbo.usp_ValidateCase`. |
| **InExpress → Timeout** | Max seconds to wait for a validation answer before rejecting. |
| **C-Cube → Server / Database** | C-Cube's own database, for scan history. Leave blank to skip history. |
| **PLC → IP / Rack / Slot** | How to reach the Siemens S7-1200. S7-1200 is usually Rack 0, Slot 1. |
| **PLC → Data block number** | The shared data block (DB) number in the PLC program. |

## Going live at the plant (checklist)

1. Run the database scripts once:
   - `database/01-ccube-schema.sql` on the **C-Cube** database.
   - PMI provides the real `usp_ValidateCase` on **InExpress** (see `database/02-inexpress-validate.sql` for the contract).
2. On the plant PC, run `.\scripts\publish.ps1` output (or copy the published folder) and start `CCube.exe`.
3. Configuration → Edit:
   - Untick **Demo mode**.
   - Fill in **InExpress** and **C-Cube** connections (use the Test buttons).
   - Fill in the **PLC** IP / rack / slot / DB number (Test reachability).
   - Change the **admin password**.
4. **Save** and **restart**. The app now validates live cases automatically.

### PLC (TIA Portal) prerequisites

For the app to read/write the data block, PMI must, on that DB:
- Turn **OFF** "Optimized block access".
- Enable **PUT/GET** communication on the CPU.

The exact byte layout the app expects is in `CCube/Plc/Db100Layout.cs` — it must match
the PLC program.
