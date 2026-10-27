using KaamLink.Application.Options;

namespace KaamLink.Api.Configuration;

/// <summary>
/// Fail-fast configuration validation. Development friction is acceptable;
/// silently insecure deployments are not.
/// </summary>
public static class StartupGuards
{
    public static void ValidateOptions(JwtOptions jwt, OtpOptions otp, SupabaseOptions? supabase = null)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
            problems.Add("Auth:Jwt:SigningKey is required and must be at least 32 characters.");
        if (jwt.AccessTokenMinutes is < 1 or > 120)
            problems.Add("Auth:Jwt:AccessTokenMinutes must be between 1 and 120.");
        if (jwt.RefreshTokenDays is < 1 or > 365)
            problems.Add("Auth:Jwt:RefreshTokenDays must be between 1 and 365.");
        if (string.IsNullOrWhiteSpace(otp.HashingKey) || otp.HashingKey.Length < 16)
            problems.Add("Otp:HashingKey is required and must be at least 16 characters.");
        if (otp.ChallengeLifetimeSeconds is < 60 or > 3600)
            problems.Add("Otp:ChallengeLifetimeSeconds must be between 60 and 3600.");
        if (otp.ResendCooldownSeconds is < 0 or > 3600)
            problems.Add("Otp:ResendCooldownSeconds must be between 0 and 3600.");

        // A half-configured Supabase integration would let the app render a
        // working-looking email CTA that always fails at the exchange endpoint.
        if (supabase is not null
            && (!string.IsNullOrWhiteSpace(supabase.Url) || !string.IsNullOrWhiteSpace(supabase.AnonKey))
            && !supabase.IsConfigured)
        {
            problems.Add("Supabase:Url and Supabase:AnonKey must both be set to valid public values.");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "Invalid configuration: " + string.Join(" | ", problems) +
                " Set them via environment variables or appsettings.local.json (see backend/.env.example).");
        }
    }

    /// <summary>Production-only hardening rules; Development/Staging keep local conveniences.</summary>
    public static void ValidateEnvironment(IHostEnvironment environment, IConfiguration configuration)
    {
        if (!environment.IsProduction())
            return;

        var otpProvider = configuration[$"{OtpOptions.SectionName}:Provider"];
        if (string.Equals(otpProvider, "log", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Otp:Provider='log' is forbidden in Production. Wire a real SMS provider before deploying.");
        }

        var supabaseUrl = configuration[$"{SupabaseOptions.SectionName}:Url"] ?? string.Empty;
        var supabaseAnonKey = configuration[$"{SupabaseOptions.SectionName}:AnonKey"] ?? string.Empty;
        if (!Uri.TryCreate(supabaseUrl, UriKind.Absolute, out var supabaseUri)
            || supabaseUri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(supabaseAnonKey))
        {
            throw new InvalidOperationException(
                "Supabase:Url (HTTPS) and Supabase:AnonKey are required in Production for email magic-link sign-in.");
        }

        var connectionString = configuration.GetConnectionString("Postgres") ?? string.Empty;
        if (connectionString.Contains("Password=kaamlink", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Password=postgres", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Default development database credentials detected in Production. Use environment-specific secrets.");
        }

        // Never allow the committed docker-compose dev placeholders to reach Production.
        var signingKey = configuration[$"{JwtOptions.SectionName}:SigningKey"] ?? string.Empty;
        var hashingKey = configuration[$"{OtpOptions.SectionName}:HashingKey"] ?? string.Empty;
        if (signingKey.Contains("dev-only", StringComparison.OrdinalIgnoreCase)
            || hashingKey.Contains("dev-only", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Development placeholder secrets detected in Production configuration. Provide real secrets.");
        }
    }
}
