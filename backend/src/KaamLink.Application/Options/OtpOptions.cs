namespace KaamLink.Application.Options;

/// <summary>Configuration for OTP challenges. Bound from the "Otp" section.</summary>
public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    /// <summary>
    /// Sender implementation. "log" = development sandbox (code is written to logs,
    /// never sent over SMS). Production startup guards reject "log" — wire a real
    /// provider (Phase 06+) before any deployed environment.
    /// </summary>
    public string Provider { get; init; } = "log";

    /// <summary>Server-side pepper for HMAC-SHA256 hashing of OTP codes. Min 16 chars; REQUIRED (validated at startup).</summary>
    public string HashingKey { get; init; } = string.Empty;

    public int ChallengeLifetimeSeconds { get; init; } = 300;

    public int ResendCooldownSeconds { get; init; } = 60;
}
