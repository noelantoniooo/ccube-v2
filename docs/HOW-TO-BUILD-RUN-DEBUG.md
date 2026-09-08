# How to build, run & debug C-Cube (for a newbie)

You don't need to be a C# expert to work on this. This guide covers the everyday actions.

## 0. One-time setup

1. **Install the .NET 10 SDK** (if `dotnet --version` in PowerShell doesn't print `10.x`).
   Get it from https://dotnet.microsoft.com/download → .NET 10.0.
2. **Pick an editor:**
   - **Visual Studio 2022 Community** (free) — *recommended*. It has the **drag-and-drop WinForms
     designer** (double-click a form, drag buttons/labels around). Install with the
     "**.NET desktop development**" workload ticked.
   - **VS Code** — lighter. Install the **C# Dev Kit** extension. Great for editing and debugging,
     but **no visual designer** — you edit `*.Designer.cs` by hand.

## 1. Open the project

- **Visual Studio:** File → Open → Project/Solution → pick `CCube.slnx`.
- **VS Code:** File → Open Folder → pick the `CCubev2` folder.

## 2. Build (compile) it

Checks your code is valid. Nothing runs.

- **Visual Studio:** Build menu → Build Solution (or `Ctrl+Shift+B`).
- **VS Code / PowerShell:**
  ```powershell
  .\scripts\build.ps1
  ```
If it fails, read the red text — it names the **file and line number**. Click it to jump there.

## 3. Run it

- **Visual Studio:** press **F5** (with debugging) or **Ctrl+F5** (just run).
- **PowerShell:**
  ```powershell
  .\scripts\start.ps1
  ```
To try it with no hardware, turn on **Demo mode** in Settings and inject a barcode.

## 4. Debug it (find out *why* something breaks)

"Debugging" = pausing the app on a line so you can look at the values.

1. Click in the left margin next to a line of code to set a **breakpoint** (a red dot).
2. Run with **F5** (Visual Studio) or **F5** (VS Code).
3. When the app reaches that line it **pauses**. Hover over any variable to see its value.
4. Step through with **F10** (next line) / **F11** (step into a method) / **F5** (continue).

This is the fastest way to understand how the code flows — set a breakpoint in `ScanEngine` and
watch a scan travel through it.

## 5. See the logs

The app writes a daily log file to `%LocalAppData%\CCube\logs\`. To watch it live:
```powershell
.\scripts\watch-log.ps1
```
Leave that window open while the app runs. Errors (failed PLC connect, SQL problems) show up here.

## 6. Make a plant build (deployable)

Produces a self-contained folder that runs on a PC with **nothing installed**:
```powershell
.\scripts\publish.ps1
```
Copy the whole `publish` folder to the plant PC and run `CCube.exe`. Then open Settings, enter the
real connection details, turn Demo off, and restart.

## 7. Editing a screen (WinForms)

Each screen is two files, e.g. `Forms\MainForm.cs` and `Forms\MainForm.Designer.cs`:
- **`.Designer.cs`** = the controls and their layout. In Visual Studio, edit this **visually** by
  double-clicking the form in Solution Explorer and dragging controls. In VS Code, edit the C# by
  hand (add `new Button()`, set `Location`/`Text`, `Controls.Add(...)`).
- **`.cs`** = the code that runs (button clicks, etc.). Double-click a button in the VS designer and
  it creates the handler for you here.

## 8. Common problems

| Symptom | Fix |
|---|---|
| `dotnet` not found | Install the .NET 10 SDK (step 0), reopen the terminal. |
| Build error "file is locked" | The app is still running. Close it, or `start.ps1` closes it for you. |
| Nothing happens on run | Check the log (`watch-log.ps1`); check Settings has valid connections. |
| Can't see the designer | You're in VS Code — use Visual Studio for the visual designer. |
