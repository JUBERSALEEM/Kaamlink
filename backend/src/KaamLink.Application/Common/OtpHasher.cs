using System.Security.Cryptography;
using System.Text;

namespace KaamLink.Application.Common;

/// <summary>
/// HMAC-SHA256 hashing of OTP codes with a server-side pepper (Otp:HashingKey).
/// Plaintext codes never touch the database; constant-time comparison on verify.
/// </summary>
public static class OtpHasher
{
    public static string Hash(string code, string hashingKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hashingKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
    }

    public static bool Verify(string code, string expectedHash, string hashingKey) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Hash(code, hashingKey)),
            Encoding.ASCII.GetBytes(expectedHash));
}
