using System.Text.RegularExpressions;

namespace KaamLink.Domain.Shared;

/// <summary>
/// Normalized E.164 phone number, e.g. "+971501234567".
/// The product targets the UAE first: a local number typed as "0501234567"
/// (10 digits starting with 05) is normalized to "+971501234567".
/// Anything else must arrive already in E.164 form.
/// </summary>
public sealed partial record PhoneNumber
{
    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    public static bool TryParse(string? raw, out PhoneNumber phoneNumber)
    {
        phoneNumber = null!;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        // Strip whitespace, dashes and parentheses people habitually type.
        var cleaned = CleanupRegex().Replace(raw.Trim(), string.Empty);

        if (cleaned.StartsWith("00", StringComparison.Ordinal))
            cleaned = "+" + cleaned[2..];

        // UAE local mobile format: 05X XXXXXXX (10 digits, starts with 05).
        if (cleaned.StartsWith("05", StringComparison.Ordinal)
            && cleaned.Length == 10
            && cleaned.All(char.IsDigit))
        {
            cleaned = "+971" + cleaned[1..];
        }

        if (!E164Regex().IsMatch(cleaned))
            return false;

        phoneNumber = new PhoneNumber(cleaned);
        return true;
    }

    public override string ToString() => Value;

    // E.164: + followed by 8-15 digits, first not zero.
    [GeneratedRegex(@"^\+[1-9]\d{7,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164Regex();

    [GeneratedRegex(@"[\s\-\(\)]", RegexOptions.CultureInvariant)]
    private static partial Regex CleanupRegex();
}
