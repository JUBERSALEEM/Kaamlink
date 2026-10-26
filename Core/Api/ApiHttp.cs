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
            client.Timeout = TimeSpan.FromSeconds(ApiConfig.TimeoutSeconds);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
