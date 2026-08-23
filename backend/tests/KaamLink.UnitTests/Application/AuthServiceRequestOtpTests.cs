using KaamLink.Application.Auth;
using KaamLink.Application.Common;
using KaamLink.Domain.Auditing;

namespace KaamLink.UnitTests.Application;

public sealed class AuthServiceRequestOtpTests
{
    private const string UaeLocalPhone = "0501234567";
    private const string NormalizedPhone = "+971501234567";

    [Fact]
    public async Task RequestOtp_CreatesChallenge_SendsHashedCode_AndAudits()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();

        var response = await service.RequestOtpAsync(new RequestOtpRequest { Phone = UaeLocalPhone });

        Assert.Equal(60, response.ResendCooldownSeconds);
        Assert.Equal(300, response.ExpiresInSeconds);

        // Sender received a 6-digit code for the normalized phone.
        var (sentPhone, sentCode) = Assert.Single(harness.OtpSender.Sent);
        Assert.Equal(NormalizedPhone, sentPhone);
        Assert.Matches(@"^\d{6}$", sentCode);

        // Only the HMAC hash is stored — never the plaintext code.
        var challenge = Assert.Single(harness.OtpChallenges.Store);
        Assert.Equal(NormalizedPhone, challenge.PhoneNumber);
        Assert.Equal(OtpHasher.Hash(sentCode, harness.OtpOptions.HashingKey), challenge.CodeHash);
        Assert.DoesNotContain(sentCode, challenge.CodeHash);

        Assert.Equal(1, harness.UnitOfWork.SaveCalls);
        Assert.Contains(harness.Audit.Records, r => r.Action == AuditActions.OtpRequested);
    }

    [Fact]
    public async Task RequestOtp_WithinCooldown_ThrowsTooManyRequests()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();
        await service.RequestOtpAsync(new RequestOtpRequest { Phone = UaeLocalPhone });

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.RequestOtpAsync(new RequestOtpRequest { Phone = UaeLocalPhone }));

        Assert.Equal("auth.otp_resend_cooldown", exception.Code);
        Assert.Equal(429, exception.StatusCode);
        Assert.NotNull(exception.Details);
        Assert.Equal(60, exception.Details!["retryAfterSeconds"]);
        Assert.Single(harness.OtpChallenges.Store);
    }

    [Fact]
    public async Task RequestOtp_AfterCooldown_CreatesNewChallenge()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();
        await service.RequestOtpAsync(new RequestOtpRequest { Phone = UaeLocalPhone });

        harness.Time.Advance(TimeSpan.FromSeconds(61));
        await service.RequestOtpAsync(new RequestOtpRequest { Phone = UaeLocalPhone });

        Assert.Equal(2, harness.OtpChallenges.Store.Count);
        Assert.Equal(2, harness.OtpSender.Sent.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("not-a-phone")]
    public async Task RequestOtp_InvalidPhone_ThrowsAndSendsNothing(string phone)
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.RequestOtpAsync(new RequestOtpRequest { Phone = phone }));

        Assert.Equal("auth.phone_invalid", exception.Code);
        Assert.Equal(400, exception.StatusCode);
        Assert.Empty(harness.OtpSender.Sent);
        Assert.Empty(harness.OtpChallenges.Store);
    }
}
