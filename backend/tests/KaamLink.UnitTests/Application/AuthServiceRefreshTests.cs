using KaamLink.Application.Auth;
using KaamLink.Application.Common;
using KaamLink.Domain.Auditing;

namespace KaamLink.UnitTests.Application;

public sealed class AuthServiceRefreshTests
{
    private const string Phone = "+971509998887";

    private static async Task<AuthTokensResponse> LoginAsync(AuthServiceHarness.Context harness)
    {
        var service = harness.CreateService();
        return await service.VerifyOtpAsync(
            new VerifyOtpRequest { Phone = Phone, Code = await harness.RequestOtpAndCaptureCodeAsync(Phone) });
    }

    [Fact]
    public async Task Refresh_ValidToken_Rotates_AndKeepsSessionAlive()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();
        var login = await LoginAsync(harness);

        var refreshed = await service.RefreshAsync(new RefreshTokenRequest { RefreshToken = login.RefreshToken });

        Assert.NotEqual(login.AccessToken, refreshed.AccessToken);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(login.User.Id, refreshed.User.Id);

        Assert.Equal(2, harness.RefreshTokens.Store.Count);
        var old = harness.RefreshTokens.Store[0];
        var replacement = harness.RefreshTokens.Store[1];
        Assert.NotNull(old.RevokedAt);
        Assert.Equal(replacement.Id, old.ReplacedByTokenId);
        Assert.Null(replacement.RevokedAt);
        Assert.Contains(harness.Audit.Records, r => r.Action == AuditActions.TokenRefreshed);
    }

    [Fact]
    public async Task Refresh_ReusedRotatedToken_RevokesWholeFamily_AndAudits()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();
        var login = await LoginAsync(harness);
        var firstRotation = await service.RefreshAsync(new RefreshTokenRequest { RefreshToken = login.RefreshToken });

        // Attacker (or buggy client) presents the already-rotated token again.
        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.RefreshAsync(new RefreshTokenRequest { RefreshToken = login.RefreshToken }));

        Assert.Equal("auth.token_reused", exception.Code);
        Assert.Equal(401, exception.StatusCode);
        Assert.Contains(harness.Audit.Records, r => r.Action == AuditActions.TokenReuseDetected);
        Assert.All(harness.RefreshTokens.Store, token => Assert.NotNull(token.RevokedAt));

        // The legitimately rotated token is dead too — full re-authentication is required.
        var followUp = await Assert.ThrowsAsync<AppException>(
            () => service.RefreshAsync(new RefreshTokenRequest { RefreshToken = firstRotation.RefreshToken }));
        Assert.Equal("auth.token_revoked", followUp.Code);
    }

    [Fact]
    public async Task Refresh_UnknownToken_ThrowsInvalid()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.RefreshAsync(new RefreshTokenRequest { RefreshToken = "does-not-exist" }));

        Assert.Equal("auth.token_invalid", exception.Code);
        Assert.Equal(401, exception.StatusCode);
    }

    [Fact]
    public async Task Refresh_EmptyToken_ThrowsBadRequest()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.RefreshAsync(new RefreshTokenRequest { RefreshToken = "  " }));

        Assert.Equal("auth.token_required", exception.Code);
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_ThrowsExpired()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();
        var login = await LoginAsync(harness);

        harness.Time.Advance(TimeSpan.FromDays(31));

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.RefreshAsync(new RefreshTokenRequest { RefreshToken = login.RefreshToken }));

        Assert.Equal("auth.token_expired", exception.Code);
    }

    [Fact]
    public async Task Refresh_BlockedUser_IsForbidden()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();
        var login = await LoginAsync(harness);

        harness.Users.Store.Single().Block(harness.Time.UtcNow);

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.RefreshAsync(new RefreshTokenRequest { RefreshToken = login.RefreshToken }));

        Assert.Equal("auth.account_blocked", exception.Code);
        Assert.Equal(403, exception.StatusCode);
    }

    [Fact]
    public async Task Logout_OwnToken_RevokesIt_AndSubsequentRefreshIsRejected()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();
        var login = await LoginAsync(harness);

        await service.LogoutAsync(login.User.Id, new LogoutRequest { RefreshToken = login.RefreshToken });

        Assert.Contains(harness.Audit.Records, r => r.Action == AuditActions.TokenRevoked);

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.RefreshAsync(new RefreshTokenRequest { RefreshToken = login.RefreshToken }));
        Assert.Equal("auth.token_revoked", exception.Code);
    }

    [Fact]
    public async Task Logout_ForeignToken_DoesNotRevoke_AndIsAudited()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();
        var login = await LoginAsync(harness);

        var attacker = KaamLink.Domain.Identity.User.Register(
            "+971500000001", isPlatformAdmin: false, harness.Time.UtcNow);
        await harness.Users.AddAsync(attacker);

        await service.LogoutAsync(attacker.Id, new LogoutRequest { RefreshToken = login.RefreshToken });

        var victimToken = Assert.Single(harness.RefreshTokens.Store);
        Assert.Null(victimToken.RevokedAt);
        Assert.Contains(harness.Audit.Records, r => r.Action == AuditActions.ForeignTokenRevokeAttempt);
    }
}
