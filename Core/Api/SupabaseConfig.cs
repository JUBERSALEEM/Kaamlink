using System.Reflection;

namespace KaamLink.Core.Api
{
    /// <summary>
    /// Public Supabase settings baked into the app at build time. These are NOT
    /// secrets: the project URL and publishable/anon key are meant to ship in a
    /// mobile client. The service-role key must never be placed here.
    ///
    /// Override with -p:KaamLinkSupabaseUrl=... and
    /// -p:KaamLinkSupabaseAnonKey=... when changing Supabase projects.
    /// </summary>
    public static class SupabaseConfig
    {
        public const string RedirectUri = "kaamlink://auth-callback";
        public const string OtpPath = "auth/v1/otp";
        public const string VerifyPath = "auth/v1/verify";
        public const string UserPath = "auth/v1/user";
        public const string TokenPath = "auth/v1/token?grant_type=password";

        private const string UrlMetadataKey = "KaamLink.SupabaseUrl";
        private const string AnonKeyMetadataKey = "KaamLink.SupabaseAnonKey";

        private static readonly Lazy<Resolution> Resolved = new(ResolveCore);

        public static string Url => Resolved.Value.Url;

        public static string AnonKey => Resolved.Value.AnonKey;

        public static bool IsConfigured => Resolved.Value.Configured;

        public static string? ConfigurationIssue => Resolved.Value.Issue;

        public static Uri OtpUri => new(new Uri(Url, UriKind.Absolute), OtpPath);

        public static Uri VerifyUri => new(new Uri(Url, UriKind.Absolute), VerifyPath);

        public static Uri UserUri => new(new Uri(Url, UriKind.Absolute), UserPath);

        public static Uri TokenUri => new(new Uri(Url, UriKind.Absolute), TokenPath);

        private sealed record Resolution(string Url, string AnonKey, bool Configured, string? Issue);

        private static Resolution ResolveCore()
        {
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                foreach (var attribute in typeof(SupabaseConfig).Assembly
                             .GetCustomAttributes<AssemblyMetadataAttribute>())
                {
                    metadata[attribute.Key] = attribute.Value ?? string.Empty;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KaamLink] Supabase build metadata could not be read: {exception}");
            }

            metadata.TryGetValue(UrlMetadataKey, out var rawUrl);
            metadata.TryGetValue(AnonKeyMetadataKey, out var anonKey);
            rawUrl = (rawUrl ?? string.Empty).Trim();
            anonKey = (anonKey ?? string.Empty).Trim();

            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps)
            {
                return new Resolution(string.Empty, anonKey, false,
                    "Set a public HTTPS Supabase URL with -p:KaamLinkSupabaseUrl=https://<project>.supabase.co.");
            }

            if (string.IsNullOrWhiteSpace(anonKey)
                || anonKey.Contains("service_role", StringComparison.OrdinalIgnoreCase))
            {
                return new Resolution(uri.AbsoluteUri, string.Empty, false,
                    "Set a publishable/anon key with -p:KaamLinkSupabaseAnonKey=<publishable-key>. Never ship a service-role key.");
            }

            return new Resolution(uri.AbsoluteUri, anonKey, true, null);
        }
    }
}
