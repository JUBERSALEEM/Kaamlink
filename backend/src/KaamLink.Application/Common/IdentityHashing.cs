using System.Security.Cryptography;
using System.Text;

namespace KaamLink.Application.Common;

public static class IdentityHashing
{
    /// <summary>Lowercase hex SHA-256 of a value (used for refresh-token storage).</summary>
    public static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
