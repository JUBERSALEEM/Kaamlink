using KaamLink.Domain.Shared;

namespace KaamLink.UnitTests.Domain;

public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("+971501234567", "+971501234567")] // E.164 passthrough
    [InlineData("0501234567", "+971501234567")] // UAE local → +971
    [InlineData("050 123 4567", "+971501234567")] // spaces stripped
    [InlineData("050-123-4567", "+971501234567")] // dashes stripped
    [InlineData("+971 50 123 4567", "+971501234567")] // E.164 with spaces
    [InlineData("00971501234567", "+971501234567")] // international 00 prefix
    [InlineData("+442071234567", "+442071234567")] // other country codes allowed (future expansion)
    public void TryParse_ValidInput_Normalizes(string input, string expected)
    {
        var ok = PhoneNumber.TryParse(input, out var phone);

        Assert.True(ok);
        Assert.Equal(expected, phone.Value);
        Assert.Equal(expected, phone.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+123")] // too short
    [InlineData("+0234567890")] // leading zero after +
    [InlineData("+97150123456789")] // too long (> 15 digits)
    [InlineData("051234567")] // UAE local but wrong length
    [InlineData("0401234567")] // not a 05x UAE mobile pattern
    [InlineData("abcdefghij")]
    [InlineData("+97150123456a")]
    [InlineData("971501234567")] // missing +
    public void TryParse_InvalidInput_ReturnsFalse(string? input)
    {
        Assert.False(PhoneNumber.TryParse(input, out _));
    }
}
