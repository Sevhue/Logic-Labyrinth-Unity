using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Utility for hashing passwords with SHA-256 + per-user random salt.
/// The salt is stored alongside the hash so it can be verified later if needed.
/// </summary>
public static class PasswordHasher
{
    private const int SALT_SIZE = 16; // 16 bytes = 128-bit salt

    /// <summary>
    /// Generates a cryptographically random salt encoded as a Base64 string.
    /// </summary>
    public static string GenerateSalt()
    {
        byte[] saltBytes = new byte[SALT_SIZE];
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(saltBytes);
        }
        return Convert.ToBase64String(saltBytes);
    }

    /// <summary>
    /// Hashes a password using SHA-256 combined with the given salt.
    /// Returns the hash as a lowercase hex string.
    /// </summary>
    public static string HashPassword(string password, string salt)
    {
        if (string.IsNullOrEmpty(password)) return "";

        // Combine salt + password
        string salted = salt + password;

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(salted));

            // Convert to hex string
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));

            return sb.ToString();
        }
    }

    /// <summary>
    /// Convenience method: generates a new salt, hashes the password, and returns both.
    /// </summary>
    public static (string hash, string salt) HashNewPassword(string password)
    {
        string salt = GenerateSalt();
        string hash = HashPassword(password, salt);
        return (hash, salt);
    }

    /// <summary>
    /// Verifies a password against a stored hash + salt.
    /// </summary>
    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
            return false;

        string computedHash = HashPassword(password, storedSalt);
        return string.Equals(computedHash, storedHash, StringComparison.OrdinalIgnoreCase);
    }
}
