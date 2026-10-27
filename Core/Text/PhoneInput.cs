using System.Text.RegularExpressions;

namespace KaamLink.Core.Text
{
    /// <summary>
    /// Normalizes a selected-country national number or a pasted international number
    /// to E.164. The server still validates the E.164 envelope authoritatively.
    /// </summary>
    public static partial class PhoneInput
    {
        /// <summary>Compatibility overload: interpret national digits as a UAE number.</summary>
        public static string? TryNormalize(string? raw) =>
            TryNormalize(raw, new CountryPhoneOption("AE", "971", "0", false, "United Arab Emirates", "United Arab Emirates"));

        /// <summary>Returns normalized E.164 or null when the result is outside E.164 bounds.</summary>
        public static string? TryNormalize(string? raw, CountryPhoneOption country)
        {
            if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrWhiteSpace(country.CallingCode))
                return null;

            // Arabic-Indic / Extended Arabic-Indic digits become ASCII before the
            // E.164 check; the API contract is an ASCII plus-prefixed number.
            var cleaned = SeparatorsRegex().Replace(DigitText.Normalize(raw).Trim(), string.Empty);
            if (cleaned.StartsWith("00", StringComparison.Ordinal))
                cleaned = "+" + cleaned[2..];

            if (!cleaned.StartsWith('+'))
            {
                if (!cleaned.All(char.IsAsciiDigit))
                    return null;

                // Accept a pasted calling code without its plus, but do not mistake
                // ordinary national input for one unless it is longer than a local number.
                if (cleaned.StartsWith(country.CallingCode, StringComparison.Ordinal)
                    && cleaned.Length > country.CallingCode.Length + 7)
                {
                    cleaned = "+" + cleaned;
                }
                else
                {
                    // Remove the national trunk prefix when metadata says it is a
                    // removable prefix. A few plans (for example Italy) retain a
                    // leading zero in the international significant number.
                    if (!country.KeepNationalPrefix
                        && country.NationalPrefix.Length > 0
                        && cleaned.StartsWith(country.NationalPrefix, StringComparison.Ordinal)
                        && cleaned.Length > country.NationalPrefix.Length + 6)
                    {
                        cleaned = cleaned[country.NationalPrefix.Length..];
                    }

                    cleaned = "+" + country.CallingCode + cleaned;
                }
            }

            return E164Regex().IsMatch(cleaned) ? cleaned : null;
        }

        // E.164: plus followed by 8–15 digits, first digit non-zero.
        [GeneratedRegex(@"^\+[1-9]\d{7,14}$", RegexOptions.CultureInvariant)]
        private static partial Regex E164Regex();

        [GeneratedRegex(@"[\s\-\(\)]", RegexOptions.CultureInvariant)]
        private static partial Regex SeparatorsRegex();
    }
}
