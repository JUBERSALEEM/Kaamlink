namespace KaamLink.Core.Api
{
    /// <summary>Decoded result of a legacy Supabase link callback.</summary>
    public sealed record EmailAuthCallback(
        string? AccessToken,
        string? RefreshToken,
        string? Type,
        string? Error)
    {
        public bool HasTokens =>
            !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(RefreshToken);

        public bool HasError => !string.IsNullOrWhiteSpace(Error);
    }

    /// <summary>
    /// Holds a legacy link callback between the Android intent and the login page.
    /// A cold start can deliver OnAppLinkRequestReceived before the login view
    /// model exists, so the parsed callback must survive until OnAppearing.
    /// </summary>
    public interface IEmailAuthLinkService
    {
        void Store(Uri uri);

        bool TryTakePending(out EmailAuthCallback callback);
    }

    public sealed class EmailAuthLinkService : IEmailAuthLinkService
    {
        private readonly object _gate = new();
        private EmailAuthCallback? _pending;

        public void Store(Uri uri)
        {
            if (!EmailAuthLinkParser.IsCallback(uri))
                return;

            var callback = EmailAuthLinkParser.Parse(uri);
            lock (_gate)
                _pending = callback;
        }

        public bool TryTakePending(out EmailAuthCallback callback)
        {
            lock (_gate)
            {
                if (_pending is null)
                {
                    callback = new EmailAuthCallback(null, null, null, null);
                    return false;
                }

                callback = _pending;
                _pending = null;
                return true;
            }
        }
    }

    public static class EmailAuthLinkParser
    {
        public static bool IsCallback(Uri uri) =>
            uri is not null
            && string.Equals(uri.Scheme, "kaamlink", StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, "auth-callback", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Supabase implicit flow returns tokens in the URL fragment. Query is
        /// also accepted for web-based redirects and error descriptions.
        /// </summary>
        public static EmailAuthCallback Parse(Uri uri)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Add(values, uri.Query);
            Add(values, uri.Fragment.TrimStart('#'));

            var error = FirstNonEmpty(
                Get(values, "error_description"),
                Get(values, "error"),
                Get(values, "error_code"));

            return new EmailAuthCallback(
                Get(values, "access_token"),
                Get(values, "refresh_token"),
                Get(values, "type"),
                error);
        }

        private static void Add(IDictionary<string, string> values, string raw)
        {
            foreach (var pair in raw.TrimStart('?', '#').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=');
                var key = separator >= 0 ? pair[..separator] : pair;
                var value = separator >= 0 ? pair[(separator + 1)..] : string.Empty;
                values[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value.Replace("+", " "));
            }
        }

        private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;

        private static string? FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
