namespace KaamLink.Api.Configuration;

/// <summary>Central registry of authorization policy names (no magic strings).</summary>
public static class AuthorizationPolicies
{
    /// <summary>Platform operators. Company membership roles (Phase 03) get their own policies.</summary>
    public const string PlatformAdmin = "PlatformAdmin";
}
