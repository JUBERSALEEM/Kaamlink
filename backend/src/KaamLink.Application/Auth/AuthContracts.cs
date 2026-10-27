using KaamLink.Domain.Identity;

namespace KaamLink.Application.Auth;

// ---- Requests (bound from JSON; invalid input is rejected by domain validation, not binders) ----

public sealed record RequestOtpRequest
{
    public string Phone { get; init; } = string.Empty;
}

public sealed record VerifyOtpRequest
{
    public string Phone { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;
}

/// <summary>
/// A Supabase magic-link access token. The backend verifies it with GoTrue and
/// exchanges it for the app's own access/refresh token pair; the Supabase token
/// is never accepted directly by KaamLink API endpoints.
/// </summary>
public sealed record SupabaseExchangeRequest
{
    public string AccessToken { get; init; } = string.Empty;
}

public sealed record RefreshTokenRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed record LogoutRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

// ---- Responses (serialized camelCase by the host) ----

public sealed record RequestOtpResponse(int ResendCooldownSeconds, int ExpiresInSeconds);

public sealed record UserSummary(
    Guid Id,
    string PhoneNumber,
    string? DisplayName,
    string? PreferredLocale,
    string Status,
    bool IsPlatformAdmin,
    DateTimeOffset CreatedAt,
    MeWorkerInfo Worker,
    IReadOnlyList<MeCompanyMembership> Memberships)
{
    public static UserSummary From(User user) => new(
        user.Id,
        user.PhoneNumber,
        user.DisplayName,
        user.PreferredLocale,
        user.Status.ToString(),
        user.IsPlatformAdmin,
        user.CreatedAt,
        MeWorkerInfo.None,
        []);

    public static UserSummary From(
        User user,
        MeWorkerInfo worker,
        IReadOnlyList<MeCompanyMembership> memberships) => new(
        user.Id,
        user.PhoneNumber,
        user.DisplayName,
        user.PreferredLocale,
        user.Status.ToString(),
        user.IsPlatformAdmin,
        user.CreatedAt,
        worker,
        memberships);
}

/// <summary>Worker-profile presence/progress shown to the app for role gating.</summary>
public sealed record MeWorkerInfo(bool HasProfile, bool OnboardingComplete, int ProfileCompletenessPct)
{
    public static readonly MeWorkerInfo None = new(false, false, 0);
}

/// <summary>A company the caller belongs to, with their role around it.</summary>
public sealed record MeCompanyMembership(Guid CompanyId, string Name, string Role, string VerificationState);

public sealed record AuthTokensResponse(
    string TokenType,
    string AccessToken,
    int AccessTokenExpiresIn,
    string RefreshToken,
    int RefreshTokenExpiresIn,
    UserSummary User);

// ---- Internal issue result between token service and use case ----

public sealed record AccessTokenIssue(string Token, int ExpiresInSeconds);
