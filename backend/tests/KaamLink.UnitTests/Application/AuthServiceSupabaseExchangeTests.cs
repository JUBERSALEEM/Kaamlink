using KaamLink.Application.Auth;
using KaamLink.Application.Common;
using KaamLink.Domain.Auditing;
using KaamLink.Domain.Identity;

namespace KaamLink.UnitTests.Application;

public sealed class AuthServiceSupabaseExchangeTests
{
    [Fact]
    public async Task Exchange_VerifiedToken_RegistersEmailUser_IssuesOwnTokens()
    {
        var harness = AuthServiceHarness.Create();
        harness.SupabaseVerifier.Email = "Worker@Example.com";
        var service = harness.CreateService();

        var response = await service.ExchangeSupabaseTokenAsync(
            new SupabaseExchangeRequest { AccessToken = "supabase-access-token" });

        Assert.Equal("Bearer", response.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        Assert.Equal(1, harness.SupabaseVerifier.Calls);

        var user = Assert.Single(harness.Users.Store);
        Assert.Equal("worker@example.com", user.Email);
        Assert.StartsWith("+999", user.PhoneNumber);
        Assert.Equal(15, user.PhoneNumber.Length); // E.164-shaped, 15 digits after '+'
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(user.Id, response.User.Id);

        var refresh = Assert.Single(harness.RefreshTokens.Store);
        Assert.Equal(IdentityHashing.Sha256Hex(response.RefreshToken), refresh.TokenHash);

        var actions = harness.Audit.Records.Select(r => r.Action).ToList();
        Assert.Contains(AuditActions.UserRegistered, actions);
        Assert.Contains(AuditActions.LoginSucceeded, actions);
    }

    [Fact]
    public async Task Exchange_SecondLogin_ReusesEmailUser()
    {
        var harness = AuthServiceHarness.Create();
        var service = harness.CreateService();

        var first = await service.ExchangeSupabaseTokenAsync(
            new SupabaseExchangeRequest { AccessToken = "token-one" });
        var second = await service.ExchangeSupabaseTokenAsync(
            new SupabaseExchangeRequest { AccessToken = "token-two" });

        Assert.Equal(first.User.Id, second.User.Id);
        Assert.Single(harness.Users.Store);
        Assert.Single(harness.Audit.Records, r => r.Action == AuditActions.UserRegistered);
        Assert.Equal(2, harness.Audit.Records.Count(r => r.Action == AuditActions.LoginSucceeded));
    }

    [Fact]
    public async Task Exchange_InvalidSupabaseToken_CreatesNoUser()
    {
        var harness = AuthServiceHarness.Create();
        harness.SupabaseVerifier.Failure = AppException.Unauthorized(
            "auth.supabase_invalid", "This sign-in link is invalid or expired.");
        var service = harness.CreateService();

        await Assert.ThrowsAsync<AppException>(() => service.ExchangeSupabaseTokenAsync(
            new SupabaseExchangeRequest { AccessToken = "stale-token" }));

        Assert.Empty(harness.Users.Store);
        Assert.Empty(harness.RefreshTokens.Store);
    }
}
