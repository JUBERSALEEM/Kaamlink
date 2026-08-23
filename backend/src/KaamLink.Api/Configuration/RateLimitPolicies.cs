namespace KaamLink.Api.Configuration;

/// <summary>Central registry of rate-limit policy names (no magic strings).</summary>
public static class RateLimitPolicies
{
    /// <summary>5 requests / 5 minutes per target phone (fallback: per IP).</summary>
    public const string OtpRequest = "otp-request";

    /// <summary>15 attempts / 5 minutes per target phone (fallback: per IP).</summary>
    public const string OtpVerify = "otp-verify";
}
