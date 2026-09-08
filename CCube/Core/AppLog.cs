// AppLog.cs
// -----------------------------------------------------------------------------
// The app's logbook. EVERYTHING important gets written here so that when
// something goes wrong you can open the log and see exactly what happened, in
// order, with timestamps - instead of guessing.
//
// Where the log goes:
//     %LocalAppData%\CCube\logs\ccube-YYYYMMDD.log   (a new file each day)
// Watch it live with:  .\scripts\watch-log.ps1
//
// How to use it anywhere in the app:
//     AppLog.Info("PLC connected");
//     AppLog.Warn("InExpress slow to answer");
//     AppLog.Error("Failed to write verdict to PLC", ex);   // pass the exception!
//
// Rule: logging must NEVER crash the app. Every write is wrapped so a logging
// problem (e.g. disk full) is silently ignored rather than taking the app down.
// -----------------------------------------------------------------------------

namespace CCube.Core;

public static class AppLog
{
    private static readonly object _gate = new(); // one writer at a time (thread-safe)

    /// <summary>
    /// Raised for every log entry, so the run screen can show a live Event Log.
    /// Arguments: (timestamp, level, message). Fires on whatever thread logged.
    /// </summary>
    public static event Action<DateTime, string, string>? Entry;

    /// <summary>Folder the log files live in.</summary>
    public static string LogDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CCube", "logs");

    /// <summary>Normal, expected events ("app started", "case PASS", ...).</summary>
    public static void Info(string message) => Write("INFO", message, null);

    /// <summary>Something odd but handled ("InExpress slow", "reconnecting").</summary>
    public static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);

    /// <summary>Something failed. Always pass the exception so we get the details.</summary>
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        DateTime now = DateTime.Now;

        // Tell the UI (Event Log) - guarded so a bad handler can't break logging.
        try
        {
            Entry?.Invoke(now, level, ex is null ? message : message + " (" + ex.Message + ")");
        }
        catch { /* ignored */ }

        try
        {
            Directory.CreateDirectory(LogDirectory);
            string file = Path.Combine(LogDirectory, $"ccube-{now:yyyyMMdd}.log");

            // One tidy line per entry; exceptions get expanded on following lines.
            string line = $"{now:yyyy-MM-dd HH:mm:ss.fff} [{level,-5}] {message}";
            if (ex is not null)
            {
                line += Environment.NewLine + "        " + ex.GetType().Name + ": " + ex.Message
                        + Environment.NewLine + ex;
            }

            lock (_gate)
            {
                File.AppendAllText(file, line + Environment.NewLine);
            }

            // Also show it in the debugger's Output window when running in an IDE.
            System.Diagnostics.Debug.WriteLine(line);
        }
        catch
        {
            // Deliberately ignored: a broken logger must never crash the station.
        }
    }
}
