// PasswordHasher.cs
// -----------------------------------------------------------------------------
// Turns the admin password into a one-way "hash" so we can CHECK it without ever
// storing the real password. When the admin sets a password we store its hash;
// when someone types a password to unlock settings, we hash what they typed and
// compare. There is no way to turn the hash back into the password.
//
// We use PBKDF2 (a slow, salted hash) - the standard, safe choice - with 100,000
// rounds so guessing is expensive. Stored as "iterations.salt.hash".
// -----------------------------------------------------------------------------

using System.Security.Cryptography;

namespace CCube.Core;

public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16; // bytes
    private const int HashSize = 32; // bytes

    /// <summary>Create a storable hash of a password.</summary>
    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>Check a typed password against a stored hash. Never throws.</summary>
    public static bool Verify(string password, string storedHash)
    {
        try
        {
            string[] parts = storedHash.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            int iterations = int.Parse(parts[0]);
            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] expected = Convert.FromBase64String(parts[2]);

            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

            // Constant-time compare so timing can't leak how much matched.
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (Exception ex)
        {
            AppLog.Error("Password verification failed.", ex);
            return false;
        }
    }
}
