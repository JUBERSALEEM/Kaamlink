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
    DateTimeOffset CreatedAt)
{
    public static UserSummary From(User user) => new(
        user.Id,
        user.PhoneNumber,
        user.DisplayName,
        user.PreferredLocale,
        user.Status.ToString(),
        user.IsPlatformAdmin,
        user.CreatedAt);
}

public sealed record AuthTokensResponse(
    string TokenType,
    string AccessToken,
    int AccessTokenExpiresIn,
    string RefreshToken,
    int RefreshTokenExpiresIn,
    UserSummary User);

// ---- Internal issue result between token service and use case ----

public sealed record AccessTokenIssue(string Token, int ExpiresInSeconds);
