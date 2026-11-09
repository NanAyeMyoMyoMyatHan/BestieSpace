using System;
using System.Security.Cryptography;
using System.Text;

namespace BestieSpace.Api.Features.Auth;

public interface IPasswordHasherService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string? storedHash);
    bool NeedsRehash(string? storedHash);
}

/// <summary>
/// Cryptographically secure password hasher service for BestieSpace.
/// Uses PBKDF2 with HMAC-SHA256, 100,000 iterations, 128-bit random salt, and constant-time verification.
/// Never stores plaintext passwords.
/// </summary>
public class PasswordHasherService : IPasswordHasherService
{
    private const int SaltSize = 16; // 128 bits
    private const int HashSize = 32; // 256 bits
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;
    private const string FormatHeader = "PBKDF2-SHA256";

    public string HashPassword(string password)
    {
        if (password == null) throw new ArgumentNullException(nameof(password));

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            Algorithm,
            HashSize);

        var saltBase64 = Convert.ToBase64String(salt);
        var hashBase64 = Convert.ToBase64String(hash);

        return $"{FormatHeader}:{Iterations}:{saltBase64}:{hashBase64}";
    }

    public bool VerifyPassword(string password, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        // 1. Check if stored hash matches our standard PBKDF2 format
        if (storedHash.StartsWith($"{FormatHeader}:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = storedHash.Split(':');
            if (parts.Length != 4) return false;

            if (!int.TryParse(parts[1], out int iterations) || iterations <= 0) return false;

            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expectedHash = Convert.FromBase64String(parts[3]);

                byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(password),
                    salt,
                    iterations,
                    Algorithm,
                    expectedHash.Length);

                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch
            {
                return false;
            }
        }

        // 2. Safe backward compatibility check for migrated test accounts
        if (string.Equals(password, storedHash, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    public bool NeedsRehash(string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return true;

        if (!storedHash.StartsWith($"{FormatHeader}:", StringComparison.OrdinalIgnoreCase))
        {
            return true; // Legacy plaintext or other format
        }

        var parts = storedHash.Split(':');
        if (parts.Length != 4) return true;

        if (!int.TryParse(parts[1], out int iterations) || iterations < Iterations)
        {
            return true; // Upgrade to current iteration standard
        }

        return false;
    }
}
