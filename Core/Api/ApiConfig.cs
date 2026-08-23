namespace KaamLink.Core.Api
{
    /// <summary>
    /// API endpoints for the app. Single source of truth — no URL strings elsewhere.
    /// Dev loop: backend runs via docker compose on the host (docs/first-boot-guide.md).
    /// </summary>
    public static class ApiConfig
    {
#if DEBUG
        // Android emulator loopback address for the host machine.
        // On a PHYSICAL device run `adb reverse tcp:8080 tcp:8080` and keep this URL
        // (the port forwards to the host). Cleartext HTTP is allowed for these
        // addresses only (Platforms/Android/Resources/xml/network_security_config.xml).
        public const string BaseUrl = "http://10.0.2.2:8080/";
#else
        // Release builds point at a hosted environment; staging/prod hosting is a
        // later phase (§52 environments). No fake default is shipped.
        public const string BaseUrl = "https://replace-me-when-environments-exist.kaamlink.invalid/";
#endif

        public static readonly Uri BaseUri = new(BaseUrl);

        /// <summary>Versioned API prefix used by every request path.</summary>
        public const string ApiPrefix = "api/v1/";
    }
}
