using System.Globalization;
using System.Text;

namespace KaamLink.Core.Text
{
    /// <summary>
    /// Normalizes user-typed numerals to ASCII digits.
    ///
    /// The app ships an Arabic (RTL) experience, and the numeric keyboards on
    /// Arabic-locale Android devices commonly produce Arabic-Indic digits
    /// (٠١٢٣٤٥٦٧٨٩, U+0660–U+0669) or Extended Arabic-Indic digits (۰–۹). .NET's
    /// <c>int.TryParse</c> and <c>PhoneInput</c>'s digit checks only accept ASCII
    /// digits, and the API contract is ASCII E.164 / plain integers, so a valid
    /// number typed in Arabic digits was previously rejected as invalid.
    ///
    /// This maps each Unicode decimal digit to its ASCII equivalent and leaves
    /// every other character untouched. It is presentation-layer input handling:
    /// no API contract changes, the wire format stays ASCII.
    /// </summary>
    public static class DigitText
    {
        /// <summary>Returns <paramref name="raw"/> with all decimal digits as ASCII 0-9.</summary>
        public static string Normalize(string? raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            StringBuilder? builder = null;
            for (var index = 0; index < raw.Length; index++)
            {
                var character = raw[index];
                // Fast path: the overwhelmingly common case is already ASCII.
                if (character is >= '0' and <= '9')
                {
                    builder?.Append(character);
                    continue;
                }

                var value = CharUnicodeInfo.GetDecimalDigitValue(character);
                if (value < 0)
                {
                    builder?.Append(character);
                    continue;
                }

                builder ??= new StringBuilder(raw.Length).Append(raw, 0, index);
                builder.Append((char)('0' + value));
            }

            return builder?.ToString() ?? raw;
        }
    }
}
