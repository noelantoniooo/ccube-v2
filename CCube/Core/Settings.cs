// Settings.cs
// -----------------------------------------------------------------------------
// The app's configuration, as plain C# objects, loaded from and saved to
// %ProgramData%\CCube\settings.json (see SettingsStore). It holds the database
// connections, the PLC address and the station id - the values that differ per
// machine.
//
// DB passwords are held in memory while the app runs but stored ENCRYPTED on disk
// (CredentialProtector). The admin password is stored only as a one-way hash
// (PasswordHasher).
// -----------------------------------------------------------------------------

using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;

namespace CCube.Core;

public sealed class AppSettings
{
    public string StationId { get; set; } = "STN1";

    // Dark colour theme (off = light).
    public bool ThemeDark { get; set; }

    // Where the daily CSV history backup is written (a folder per month is created
    // underneath). Edit in settings.json to point at a shared/plant location.
    public string HistoryCsvFolder { get; set; } = @"C:\CCube\History";

    public InExpressSettings InExpress { get; set; } = new();
    public CCubeSettings CCube { get; set; } = new();
    public PlcSettings Plc { get; set; } = new();
    public AdminSettings Admin { get; set; } = new();
}

/// <summary>Connection to the plant's InExpress (MES) database.</summary>
public sealed class InExpressSettings
{
    public string Server { get; set; } = "";
    public string Database { get; set; } = "";
    public bool IntegratedSecurity { get; set; } = true; // use the Windows login
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string ProcName { get; set; } = "dbo.usp_ValidateCase";
    public int TimeoutSeconds { get; set; } = 2;

    [JsonIgnore] // computed, not stored
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(Database);

    public string BuildConnectionString() =>
        SqlConnStrings.Build(Server, Database, IntegratedSecurity, Username, Password);
}

/// <summary>Connection to C-Cube's own database (history / audit).</summary>
public sealed class CCubeSettings
{
    public string Server { get; set; } = "";
    public string Database { get; set; } = "";
    public bool IntegratedSecurity { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    [JsonIgnore] // computed, not stored in the file
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(Database);

    public string BuildConnectionString() =>
        SqlConnStrings.Build(Server, Database, IntegratedSecurity, Username, Password);
}

/// <summary>How to reach the Siemens S7-1200 PLC.</summary>
public sealed class PlcSettings
{
    public string IpAddress { get; set; } = "192.168.0.1";
    public int Rack { get; set; } = 0;
    public int Slot { get; set; } = 1;      // S7-1200/1500 typically slot 1
    public int DbNumber { get; set; } = 100; // the shared data block number
}

/// <summary>The single admin password that unlocks editing the settings.</summary>
public sealed class AdminSettings
{
    // A PBKDF2 hash, never the password itself. Empty until first set.
    public string PasswordHash { get; set; } = "";
}

// Small helper so both DB settings build their connection string the same way.
internal static class SqlConnStrings
{
    public static string Build(string server, string database, bool integrated,
        string username, string password)
    {
        var b = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            ConnectTimeout = 5,
            // Internal plant SQL servers usually don't have public certificates,
            // so we trust the server cert rather than fail to connect.
            TrustServerCertificate = true,
            Encrypt = false,
        };
        if (integrated)
        {
            b.IntegratedSecurity = true;
        }
        else
        {
            b.UserID = username;
            b.Password = password;
        }
        return b.ConnectionString;
    }
}
