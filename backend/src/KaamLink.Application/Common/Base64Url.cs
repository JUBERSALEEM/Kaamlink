using System.Security.Cryptography;

namespace KaamLink.Application.Common;

public static class Base64Url
{
    public static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Cryptographically random opaque token (default 32 bytes → 43 chars).</summary>
    public static string RandomToken(int byteLength = 32) => Encode(RandomNumberGenerator.GetBytes(byteLength));
}
