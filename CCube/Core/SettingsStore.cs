// SettingsStore.cs
// -----------------------------------------------------------------------------
// Loads and saves the app's settings to a JSON file at:
//     %ProgramData%\CCube\settings.json
//
// ProgramData is used (not next to the .exe) because a locked-down plant PC
// often won't let a normal user write into Program Files. ProgramData is shared
// per-machine and writable.
//
// On save, the two DB passwords are ENCRYPTED (CredentialProtector). On load,
// they're decrypted back for the app to use. The admin password is stored only
// as a hash (PasswordHasher). First run creates the file with default values.
// -----------------------------------------------------------------------------

using System.Text.Json;

namespace CCube.Core;

public static class SettingsStore
{
    /// <summary>The admin password a brand-new install starts with. Change it in-app.</summary>
    public const string DefaultAdminPassword = "Admin#2026";

    public static string SettingsFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CCube");

    public static string FilePath { get; } = Path.Combine(SettingsFolder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Load settings (creating defaults on first run). Never throws.</summary>
    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                AppLog.Info("No settings file found; creating defaults (Demo mode).");
                AppSettings defaults = CreateDefaults();
                Save(defaults);
                return defaults;
            }

            string json = File.ReadAllText(FilePath);
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                                   ?? CreateDefaults();

            // Decrypt the DB passwords for use while the app runs.
            settings.InExpress.Password = CredentialProtector.Unprotect(settings.InExpress.Password);
            settings.CCube.Password = CredentialProtector.Unprotect(settings.CCube.Password);

            // Safety net: never leave the admin gate with no password set.
            if (string.IsNullOrEmpty(settings.Admin.PasswordHash))
            {
                settings.Admin.PasswordHash = PasswordHasher.Hash(DefaultAdminPassword);
            }

            AppLog.Info($"Settings loaded (Station='{settings.StationId}').");
            return settings;
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to load settings; falling back to defaults.", ex);
            return CreateDefaults();
        }
    }

    /// <summary>Save settings (encrypting DB passwords on disk). Throws on failure
    /// so the UI can tell the user it didn't save.</summary>
    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);

            // Temporarily swap in encrypted passwords for writing, then restore the
            // plaintext so the running app keeps working.
            string inExpressPlain = settings.InExpress.Password;
            string ccubePlain = settings.CCube.Password;
            settings.InExpress.Password = CredentialProtector.Protect(inExpressPlain);
            settings.CCube.Password = CredentialProtector.Protect(ccubePlain);

            string json = JsonSerializer.Serialize(settings, JsonOptions);

            settings.InExpress.Password = inExpressPlain;
            settings.CCube.Password = ccubePlain;

            File.WriteAllText(FilePath, json);
            AppLog.Info($"Settings saved to {FilePath}.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to save settings.", ex);
            throw;
        }
    }

    private static AppSettings CreateDefaults()
    {
        var settings = new AppSettings();
        settings.Admin.PasswordHash = PasswordHasher.Hash(DefaultAdminPassword);
        return settings;
    }
}
