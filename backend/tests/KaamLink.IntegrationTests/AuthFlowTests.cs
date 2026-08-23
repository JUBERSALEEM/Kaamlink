using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KaamLink.Application.Auth;
using KaamLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KaamLink.IntegrationTests;

/// <summary>
/// End-to-end proof of the Phase 02 Definition of Done:
/// register (OTP) → verify → token pair → authorized request → refresh/rotate → logout.
/// Runs against real PostgreSQL; a fresh phone number per test keeps rate-limit
/// partitions and OTP challenges isolated.
/// </summary>
public sealed class AuthFlowTests : IClassFixture<KaamLinkApiFactory>
{
    private readonly KaamLinkApiFactory _factory;
    private readonly HttpClient _client;

    public AuthFlowTests(KaamLinkApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ViaOtp_Then_Me_ReturnsIdentity()
    {
        var phone = TestPhones.Next();

        var tokens = await RegisterAsync(phone);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var meResponse = await _client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = await meResponse.Content.ReadFromJsonAsync<UserSummary>();
        Assert.NotNull(me);
        Assert.Equal(phone, me.PhoneNumber);
        Assert.Equal("Active", me.Status);
    }

    [Fact]
    public async Task Me_WithoutToken_IsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Verify_WithWrongCode_ReturnsProblemWithStableCode()
    {
        var phone = TestPhones.Next();
        await RequestOtpAsync(phone);

        var code = _factory.OtpSender.GetCodeFor(phone);
        var wrongCode = code == "000000" ? "000001" : "000000";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone, code = wrongCode });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("auth.otp_invalid", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task Verify_WithoutChallenge_ReturnsProblem()
    {
        var phone = TestPhones.Next();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone, code = "123456" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("auth.otp_not_found_or_expired", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task Refresh_RotatesTokens_And_ReuseRevokesTheWholeFamily()
    {
        var phone = TestPhones.Next();
        var tokens = await RegisterAsync(phone);

        // Normal rotation: old refresh token → new pair.
        var rotatedResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/token/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, rotatedResponse.StatusCode);
        var rotated = await rotatedResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        Assert.NotNull(rotated);
        Assert.NotEqual(tokens.RefreshToken, rotated.RefreshToken);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rotated.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(meRequest)).StatusCode);

        // Presenting the already-rotated token again = reuse detected.
        var reuseResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/token/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
        Assert.Equal("auth.token_reused", await ReadProblemCodeAsync(reuseResponse));

        // Family nuked: even the legitimate latest token is dead.
        var afterReuse = await _client.PostAsJsonAsync(
            "/api/v1/auth/token/refresh", new { refreshToken = rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
        Assert.Equal("auth.token_revoked", await ReadProblemCodeAsync(afterReuse));
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var phone = TestPhones.Next();
        var tokens = await RegisterAsync(phone);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout")
        {
            Content = JsonContent.Create(new { refreshToken = tokens.RefreshToken })
        };
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(logoutRequest)).StatusCode);

        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/token/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
        Assert.Equal("auth.token_revoked", await ReadProblemCodeAsync(refreshResponse));
    }

    [Fact]
    public async Task BlockedUser_CannotLogin()
    {
        var phone = TestPhones.Next();
        await RegisterAsync(phone);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.PhoneNumber == phone);
            user.Block(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        _factory.OtpSender.Clear();
        await RequestOtpAsync(phone);
        var code = _factory.OtpSender.GetCodeFor(phone);
        var response = await _client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone, code });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("auth.account_blocked", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task OtpRequest_RateLimit_KicksIn_AfterPolicyLimit()
    {
        var phone = TestPhones.Next();

        for (var attempt = 0; attempt < 5; attempt++)
            await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone });

        var rejected = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone });

        Assert.Equal((HttpStatusCode)429, rejected.StatusCode);
        Assert.Equal("rate.limit_exceeded", await ReadProblemCodeAsync(rejected));
    }

    [Fact]
    public async Task SecurityEvents_AreWrittenToAuditLog()
    {
        var phone = TestPhones.Next();
        await RegisterAsync(phone);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var actions = await db.AuditLogs
            .Where(a => a.MetadataJson != null && a.MetadataJson.Contains(phone) || a.EntityId == phone)
            .Select(a => a.Action)
            .ToListAsync();

        Assert.Contains("auth.otp_requested", actions);
        Assert.Contains("auth.login_succeeded", actions);

        // Hard rule (§47): nothing sensitive is ever persisted — no codes, no raw tokens.
        var metadata = await db.AuditLogs.Select(a => a.MetadataJson).ToListAsync();
        Assert.All(metadata, m => Assert.False(m != null && m.Contains("code\":\"")));
    }

    // ---- helpers ----

    private async Task RequestOtpAsync(string phone)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<AuthTokensResponse> RegisterAsync(string phone)
    {
        await RequestOtpAsync(phone);
        var code = _factory.OtpSender.GetCodeFor(phone);

        var verifyResponse = await _client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var tokens = await verifyResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        Assert.NotNull(tokens);
        return tokens;
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}

internal static class TestPhones
{
    private static int _counter = 1000000;

    /// <summary>Unique valid UAE mobile number per call: +97150XXXXXXX.</summary>
    public static string Next() => $"+97150{Interlocked.Increment(ref _counter)}";
}
