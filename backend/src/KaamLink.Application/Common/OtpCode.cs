using System.Globalization;
using System.Security.Cryptography;

namespace KaamLink.Application.Common;

public static class OtpCode
{
    /// <summary>Cryptographically random 6-digit code (zero-padded).</summary>
    public static string Generate() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
}
