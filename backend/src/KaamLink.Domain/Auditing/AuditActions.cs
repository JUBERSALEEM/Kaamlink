namespace KaamLink.Domain.Auditing;

/// <summary>
/// Central registry of audited action names — audit action strings never appear
/// as magic strings elsewhere in the codebase.
/// </summary>
public static class AuditActions
{
    public const string UserRegistered = "auth.user_registered";
    public const string OtpRequested = "auth.otp_requested";
    public const string LoginSucceeded = "auth.login_succeeded";
    public const string LoginFailed = "auth.login_failed";
    public const string TokenRefreshed = "auth.token_refreshed";
    public const string TokenReuseDetected = "auth.token_reuse_detected";
    public const string TokenRevoked = "auth.token_revoked";
    public const string ForeignTokenRevokeAttempt = "auth.token_foreign_revoke_attempt";
}
