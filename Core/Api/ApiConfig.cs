using Microsoft.Maui.Devices;

namespace KaamLink.Core.Api
{
    /// <summary>
    /// API endpoints for the app. Single source of truth — no URL strings elsewhere.
    /// Dev loop: backend runs via docker compose on the host (docs/first-boot-guide.md).
    /// </summary>
    public static class ApiConfig
    {
#if DEBUG
        /// <summary>The Android emulator's alias for the host machine's loopback.</summary>
        public const string EmulatorBaseUrl = "http://10.0.2.2:8080/";

        /// <summary>
        /// What a PHYSICAL phone uses. 10.0.2.2 does not exist off the emulator, so a
        /// phone must talk to its own loopback, which `adb reverse tcp:8080 tcp:8080`
        /// forwards to the host machine. Without that forward the app correctly reports
        /// "no connection" — see docs/first-boot-guide.md §2.
        /// Both hosts are permitted cleartext in
        /// Platforms/Android/Resources/xml/network_security_config.xml, and
        /// tools/verify-compile-surface.py fails the build if that ever stops being true.
        /// </summary>
        public const string DeviceBaseUrl = "http://localhost:8080/";

        public static string BaseUrl => IsEmulator ? EmulatorBaseUrl : DeviceBaseUrl;

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
#else
        // Release builds point at a hosted environment; staging/prod hosting is a
        // later phase (§52 environments). No fake default is shipped.
        public static string BaseUrl =>
            "https://replace-me-when-environments-exist.kaamlink.invalid/";
#endif

        /// <summary>
        /// Resolved once at first use: the device type cannot change while the app runs,
        /// and every HttpClient BaseAddress must be stable.
        /// </summary>
        public static readonly Uri BaseUri = new(BaseUrl);

        /// <summary>Versioned API prefix used by every request path.</summary>
        public const string ApiPrefix = "api/v1/";
    }
}
