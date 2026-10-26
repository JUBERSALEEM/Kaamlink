using System.Reflection;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace KaamLink.Core.Api
{
    /// <summary>
    /// API endpoints for the app. Single source of truth — no URL strings elsewhere.
    ///
    /// Release builds NO LONGER ship the old reserved placeholder host. They read a
    /// public HTTPS base URL baked in at build time with
    /// <c>-p:KaamLinkApiBaseUrl=https://api.example.com/</c>. An unconfigured Release
    /// build is reported cleanly by the UI instead of silently failing DNS.
    ///
    /// Debug keeps the fast local loop (emulator host loopback or adb reverse), and a
    /// Debug-only LAN override can be supplied either through the same build property
    /// or <see cref="DebugOverrideKey"/> in Preferences. Debug is the only build whose
    /// Android network policy allows arbitrary cleartext hosts.
    /// </summary>
    public static class ApiConfig
    {
        public const string ApiPrefix = "api/v1/";
        public const int TimeoutSeconds = 15;

        private const string BuildMetadataKey = "KaamLink.ApiBaseUrl";
        public const string DebugOverrideKey = "kaamlink.api.baseUrl";

        /// <summary>Syntactically valid, non-routable fail-fast sentinel. Never sent to.</summary>
        private const string UnconfiguredBaseUrl = "https://127.0.0.1:9/";

#if DEBUG
        /// <summary>The Android emulator's alias for the host machine's loopback.</summary>
        public const string EmulatorBaseUrl = "http://10.0.2.2:8080/";

        /// <summary>
        /// What a PHYSICAL phone uses when USB reverse port forwarding is active:
        /// <c>adb reverse tcp:8080 tcp:8080</c>. Both hosts are permitted cleartext
        /// in Platforms/Android/Resources/xml/network_security_config.xml.
        /// </summary>
        public const string DeviceBaseUrl = "http://localhost:8080/";
#endif

        private static readonly Lazy<Resolution> Resolved = new(ResolveCore);

        public static string BaseUrl => Resolved.Value.Url;

        public static Uri BaseUri => new(BaseUrl, UriKind.Absolute);

        public static bool IsConfigured => Resolved.Value.Configured;

        public static string ConfigurationSource => Resolved.Value.Source;

        public static string? ConfigurationIssue => Resolved.Value.Issue;

        public static string EndpointHost => BaseUri.Host;

#if DEBUG
        private static bool IsEmulator
        {
            get
            {
                try
                {
                    return DeviceInfo.Current.DeviceType == DeviceType.Virtual;
                }
                catch
                {
                    // Essentials unavailable (a plain unit-test host). The emulator
                    // address is the one that needs no extra setup, so prefer it.
                    return true;
                }
            }
        }
#endif

        private sealed record Resolution(string Url, bool Configured, string Source, string? Issue);

        private static Resolution ResolveCore()
        {
            var buildUrl = ReadBuildConfiguredUrl();
            if (!string.IsNullOrWhiteSpace(buildUrl))
            {
                var normalized = Normalize(buildUrl!);
                if (TryValidate(normalized, out var issue))
                    return new Resolution(normalized, true, "build metadata", null);
                return new Resolution(UnconfiguredBaseUrl, false, "invalid build metadata", issue);
            }

#if DEBUG
            var overrideUrl = ReadDebugOverride();
            if (!string.IsNullOrWhiteSpace(overrideUrl))
            {
                var normalized = Normalize(overrideUrl!);
                if (TryValidate(normalized, out var overrideIssue))
                    return new Resolution(normalized, true, "Debug override", null);
                return new Resolution(UnconfiguredBaseUrl, false, "invalid Debug override", overrideIssue);
            }

            return new Resolution(
                IsEmulator ? EmulatorBaseUrl : DeviceBaseUrl,
                true,
                IsEmulator ? "Debug emulator loopback" : "Debug device loopback",
                null);
#else
            return new Resolution(
                UnconfiguredBaseUrl,
                false,
                "not configured",
                "Set a public HTTPS API URL with -p:KaamLinkApiBaseUrl=https://api.example.com/ when building Release.");
#endif
        }

        private static string? ReadBuildConfiguredUrl()
        {
            try
            {
                foreach (var attribute in typeof(ApiConfig).Assembly
                             .GetCustomAttributes<AssemblyMetadataAttribute>())
                {
                    if (string.Equals(attribute.Key, BuildMetadataKey, StringComparison.Ordinal))
                        return attribute.Value;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KaamLink] API build metadata could not be read: {exception}");
            }

            return null;
        }

        private static string? ReadDebugOverride()
        {
            try
            {
                return Preferences.Default.Get(DebugOverrideKey, string.Empty);
            }
            catch
            {
                // Preferences is unavailable in unit-test hosts; the local loop still works.
                return null;
            }
        }

        private static string Normalize(string url) =>
            url.Trim().EndsWith('/') ? url.Trim() : url.Trim() + "/";

        private static bool TryValidate(string url, out string? issue)
        {
            issue = null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || string.IsNullOrWhiteSpace(uri.Host))
            {
                issue = "The API base URL must be an absolute http(s) URL with a trailing slash.";
                return false;
            }

            if (uri.Host.Contains("replace-me", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith("invalid", StringComparison.OrdinalIgnoreCase))
            {
                issue = "The API base URL is still a placeholder host.";
                return false;
            }

            var loopback = uri.Host is "10.0.2.2" or "localhost" or "127.0.0.1";
            if (uri.Scheme == Uri.UriSchemeHttp && !loopback)
            {
#if DEBUG
                // Debug-only Android network_security_config_debug.xml permits LAN hosts.
                return true;
#else
                issue = "Release builds require HTTPS for any non-loopback API host.";
                return false;
#endif
            }

#if DEBUG
            return true;
#else
            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                issue = "Release builds require an HTTPS API base URL.";
                return false;
            }

            return true;
#endif
        }
    }
}
