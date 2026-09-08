// Program.cs
// -----------------------------------------------------------------------------
// This is where the app starts. When you double-click CCube.exe, the Main()
// method below runs first. Its job is small and deliberate:
//   1. Make sure only ONE copy of C-Cube runs at a time (a "mutex").
//   2. Turn on logging + catch any crash so it lands in the log file.
//   3. Open the main operator screen (MainForm).
//
// We keep this file tiny on purpose - the real work lives in the Forms/, Core/,
// Plc/ and Data/ folders. Start reading there.
// -----------------------------------------------------------------------------

using CCube.Core;
using CCube.Forms;

namespace CCube;

// (Theme.Dark is set from settings in Main, before any window is built.)

internal static class Program
{
    // [STAThread] is required by WinForms.
    [STAThread]
    private static void Main()
    {
        // --- Only one C-Cube at a time -----------------------------------------
        // A "mutex" is a named flag shared across the whole machine. If a second
        // copy starts, it can't create the flag, so we show a message and quit.
        // (Two apps answering the same PLC would be dangerous.)
        using var oneInstance = new Mutex(initiallyOwned: true, "CCube.Station", out bool isFirstCopy);
        if (!isFirstCopy)
        {
            MessageBox.Show("C-Cube is already running.", "C-Cube",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        AppLog.Info("=================================================================");
        AppLog.Info($"C-Cube starting (version {Application.ProductVersion}).");

        // --- Catch-all crash logging -------------------------------------------
        // If anything unexpected blows up, we want it in the log, not lost. These
        // two handlers cover background threads and the UI thread respectively.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLog.Error("FATAL (background thread).", e.ExceptionObject as Exception);

        Application.ThreadException += (_, e) =>
            AppLog.Error("Unhandled UI error.", e.Exception);

        try
        {
            // Pick the colour theme BEFORE any window is built.
            Theme.Dark = SettingsStore.Load().ThemeDark;

            // --- Standard WinForms start-up ------------------------------------
            ApplicationConfiguration.Initialize(); // sets high-DPI + visual styles
            Application.Run(new MainForm());        // opens the main window and waits
        }
        catch (Exception ex)
        {
            // Last line of defence: log it and tell the user, don't just vanish.
            AppLog.Error("C-Cube failed to start.", ex);
            MessageBox.Show("C-Cube failed to start:\n\n" + ex.Message
                + "\n\nSee the log for details:\n" + AppLog.LogDirectory,
                "C-Cube", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            AppLog.Info("C-Cube stopped.");
        }
    }
}
