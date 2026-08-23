namespace KaamLink.Application.Options;

/// <summary>Configuration for access/refresh tokens. Bound from the "Auth:Jwt" section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Auth:Jwt";

    public string Issuer { get; init; } = "kaamlink-api";

    public string Audience { get; init; } = "kaamlink-app";

    /// <summary>Symmetric HMAC key (single-service monolith; ADR 0002). Min 32 chars; REQUIRED (validated at startup).</summary>
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Short-lived by design; clients use the refresh flow.</summary>
    public int AccessTokenMinutes { get; init; } = 15;

    public int RefreshTokenDays { get; init; } = 30;
}
