using System.Text.RegularExpressions;

namespace KaamLink.Core.Text
{
    /// <summary>
    /// Client-side phone normalization for the login form — deliberately mirrors
    /// backend/src/KaamLink.Domain/Shared/PhoneNumber.cs (05XXXXXXXX → +971…, E.164 check).
    /// The backend re-validates authoritatively; this only gives fast, kind feedback.
    /// </summary>
    public static partial class PhoneInput
    {
        /// <summary>Normalizes to E.164 (e.g. +971501234567) or returns null when invalid.</summary>
        public static string? TryNormalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            // Arabic-Indic / Extended Arabic-Indic digits become ASCII first: the
            // digit checks below and the E.164 pattern are ASCII-only, and the API
            // contract expects ASCII E.164.
            var cleaned = CleanupRegex().Replace(DigitText.Normalize(raw).Trim(), string.Empty);

            if (cleaned.StartsWith("00", StringComparison.Ordinal))
                cleaned = "+" + cleaned[2..];

            // UAE local mobile: 05X XXXXXXX (10 digits starting with 05).
            if (cleaned.StartsWith("05", StringComparison.Ordinal)
                && cleaned.Length == 10
                && cleaned.All(char.IsDigit))
            {
                cleaned = "+971" + cleaned[1..];
            }
            // Common shorthand: 9715XXXXXXXX or bare 5XXXXXXXX.
            else if (cleaned.StartsWith("9715", StringComparison.Ordinal)
                && cleaned.Length == 12
                && cleaned.All(char.IsDigit))
            {
                cleaned = "+" + cleaned;
            }
            else if (cleaned.StartsWith('5')
                && cleaned.Length == 9
                && cleaned.All(char.IsDigit))
            {
                cleaned = "+971" + cleaned;
            }

            return E164Regex().IsMatch(cleaned) ? cleaned : null;
        }

        // Same contract as the backend: + followed by 8-15 digits, first not zero.
        [GeneratedRegex(@"^\+[1-9]\d{7,14}$", RegexOptions.CultureInvariant)]
        private static partial Regex E164Regex();

        [GeneratedRegex(@"[\s\-\(\)]", RegexOptions.CultureInvariant)]
        private static partial Regex CleanupRegex();
    }
}
