// CredentialProtector.cs
// -----------------------------------------------------------------------------
// Encrypts and decrypts the database passwords we save to settings.json, using
// Windows DPAPI (Data Protection API). This means:
//   * the password on disk is scrambled, not readable in a text editor, and
//   * it can only be decrypted on THIS machine (copying the file elsewhere is
//     useless), because we use the LocalMachine scope.
//
// Encrypted values are marked with an "enc:" prefix so we can tell them apart
// from a value someone typed by hand into the file.
//
// This is for secrets we need to get BACK (to build a connection string). For
// the admin password - which we only ever CHECK - we use one-way hashing
// instead (see PasswordHasher).
// -----------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

namespace CCube.Core;

public static class CredentialProtector
{
    private const string Prefix = "enc:";

    /// <summary>Encrypt a secret for storage. Empty in, empty out.</summary>
    public static string Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return "";
        }
        try
        {
            byte[] encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(plainText), optionalEntropy: null,
                scope: DataProtectionScope.LocalMachine);
            return Prefix + Convert.ToBase64String(encrypted);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to encrypt a secret; storing empty instead.", ex);
            return "";
        }
    }

    /// <summary>Decrypt a stored secret. If it isn't encrypted, return it as-is
    /// (so a hand-typed value in the file still works).</summary>
    public static string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return "";
        }
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return stored; // plain text - someone edited the file directly
        }
        try
        {
            byte[] encrypted = Convert.FromBase64String(stored[Prefix.Length..]);
            byte[] plain = ProtectedData.Unprotect(encrypted, optionalEntropy: null,
                scope: DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to decrypt a saved secret (returning empty).", ex);
            return "";
        }
    }
}
