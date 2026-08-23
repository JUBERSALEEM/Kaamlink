using KaamLink.Application.Auth;
using KaamLink.Application.Common;
using KaamLink.Domain.Auditing;
using KaamLink.Domain.Identity;

namespace KaamLink.UnitTests.Application;

public sealed class AuthServiceVerifyOtpTests
{
    private const string Phone = "+971501234567";

    [Fact]
    public async Task Verify_ValidCode_RegistersUser_IssuesTokens_Audits()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();
        var code = await harness.RequestOtpAndCaptureCodeAsync(Phone);

        var response = await service.VerifyOtpAsync(new VerifyOtpRequest { Phone = Phone, Code = code });

        Assert.Equal("Bearer", response.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        Assert.Equal(15 * 60, response.AccessTokenExpiresIn);
        Assert.Equal(30 * 24 * 60 * 60, response.RefreshTokenExpiresIn);

        var user = Assert.Single(harness.Users.Store);
        Assert.Equal(Phone, user.PhoneNumber);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(user.Id, response.User.Id);

        // Challenge consumed exactly once.
        var challenge = Assert.Single(harness.OtpChallenges.Store);
        Assert.True(challenge.IsConsumed);

        // Only the hash of the refresh token is stored.
        var refresh = Assert.Single(harness.RefreshTokens.Store);
        Assert.Equal(IdentityHashing.Sha256Hex(response.RefreshToken), refresh.TokenHash);
        Assert.DoesNotContain(response.RefreshToken, refresh.TokenHash);
        Assert.Equal(user.Id, refresh.UserId);

        var actions = harness.Audit.Records.Select(r => r.Action).ToList();
        Assert.Contains(AuditActions.UserRegistered, actions);
        Assert.Contains(AuditActions.LoginSucceeded, actions);
        Assert.True(actions.IndexOf(AuditActions.UserRegistered) < actions.IndexOf(AuditActions.LoginSucceeded));
    }

    [Fact]
    public async Task Verify_SecondLogin_ReusesUser_WithoutReRegistering()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();

        var first = await service.VerifyOtpAsync(
            new VerifyOtpRequest { Phone = Phone, Code = await harness.RequestOtpAndCaptureCodeAsync(Phone) });

        harness.Time.Advance(TimeSpan.FromSeconds(61)); // pass resend cooldown
        var second = await service.VerifyOtpAsync(
            new VerifyOtpRequest { Phone = Phone, Code = await harness.RequestOtpAndCaptureCodeAsync(Phone) });

        Assert.Equal(first.User.Id, second.User.Id);
        Assert.Single(harness.Users.Store);
        Assert.Single(harness.Audit.Records, r => r.Action == AuditActions.UserRegistered);
        Assert.Equal(2, harness.Audit.Records.Count(r => r.Action == AuditActions.LoginSucceeded));
    }

    [Fact]
    public async Task Verify_WrongCode_RegistersFailedAttempt_WithoutConsuming()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();
        await harness.RequestOtpAndCaptureCodeAsync(Phone);

        var wrongCode = harness.OtpSender.LastCode == "000000" ? "000001" : "000000";
        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.VerifyOtpAsync(new VerifyOtpRequest { Phone = Phone, Code = wrongCode }));

        Assert.Equal("auth.otp_invalid", exception.Code);
        Assert.Equal(400, exception.StatusCode);

        var challenge = Assert.Single(harness.OtpChallenges.Store);
        Assert.Equal(1, challenge.AttemptCount);
        Assert.False(challenge.IsConsumed);
        Assert.Empty(harness.Users.Store);
        Assert.Contains(harness.Audit.Records, r => r.Action == AuditActions.LoginFailed);
    }

    [Fact]
    public async Task Verify_ExhaustedAttempts_LocksChallenge()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();
        var code = await harness.RequestOtpAndCaptureCodeAsync(Phone);

        var wrongCode = code == "000000" ? "000001" : "000000";
        for (var attempt = 0; attempt < OtpChallenge.MaxAttempts; attempt++)
        {
            await Assert.ThrowsAsync<AppException>(
                () => service.VerifyOtpAsync(new VerifyOtpRequest { Phone = Phone, Code = wrongCode }));
        }

        // Even the correct code is now rejected — the challenge is locked.
        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.VerifyOtpAsync(new VerifyOtpRequest { Phone = Phone, Code = code }));

        Assert.Equal("auth.otp_locked", exception.Code);
        Assert.Empty(harness.Users.Store);
    }

    [Fact]
    public async Task Verify_ExpiredChallenge_ThrowsNotFoundOrExpired()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();
        var code = await harness.RequestOtpAndCaptureCodeAsync(Phone);

        harness.Time.Advance(TimeSpan.FromSeconds(301)); // past the 300s lifetime

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.VerifyOtpAsync(new VerifyOtpRequest { Phone = Phone, Code = code }));

        Assert.Equal("auth.otp_not_found_or_expired", exception.Code);
        Assert.Empty(harness.Users.Store);
    }

    [Fact]
    public async Task Verify_BlockedUser_IsForbidden_AndAudited()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();

        await service.VerifyOtpAsync(
            new VerifyOtpRequest { Phone = Phone, Code = await harness.RequestOtpAndCaptureCodeAsync(Phone) });

        var user = harness.Users.Store.Single();
        user.Block(harness.Time.UtcNow);

        harness.Time.Advance(TimeSpan.FromSeconds(61));
        var code = await harness.RequestOtpAndCaptureCodeAsync(Phone);
        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.VerifyOtpAsync(new VerifyOtpRequest { Phone = Phone, Code = code }));

        Assert.Equal("auth.account_blocked", exception.Code);
        Assert.Equal(403, exception.StatusCode);
        Assert.Contains(harness.Audit.Records,
            r => r.Action == AuditActions.LoginFailed && r.MetadataJson != null && r.MetadataJson.Contains("account_blocked"));
    }
}
