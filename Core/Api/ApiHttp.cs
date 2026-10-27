using System.Net.Http.Headers;

namespace KaamLink.Core.Api
{
    /// <summary>
    /// One configuration path for every KaamLink HttpClient. A missing timeout was
    /// a major reason a dead host felt like a frozen app: HttpClient's default is
    /// 100 seconds. Both the typed API client and the session auth client use this.
    /// </summary>
    public static class ApiHttp
    {
        public const string AuthClientName = "kaamlink-auth";

        public static void Configure(HttpClient client)
        {
            client.BaseAddress = ApiConfig.BaseUri;
            ConfigureCommon(client);
        }

        /// <summary>
        /// Supabase GoTrue uses a different HTTPS host but the same bounded
        /// timeout and JSON accept header as the KaamLink API client.
        /// </summary>
        public static void ConfigureSupabase(HttpClient client)
        {
            var baseUrl = SupabaseConfig.IsConfigured
                ? SupabaseConfig.Url.TrimEnd('/') + "/"
                : "https://127.0.0.1:9/";
            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            ConfigureCommon(client);
        }

        private static void ConfigureCommon(HttpClient client)
        {
            client.Timeout = TimeSpan.FromSeconds(ApiConfig.TimeoutSeconds);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
