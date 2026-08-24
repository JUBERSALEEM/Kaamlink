using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace KaamLink.Core.Api
{
    /// <summary>
    /// Attaches the bearer token to API calls and performs exactly ONE silent
    /// refresh + retry when the server answers 401. If the retry also fails the
    /// response passes through untouched (view models treat it as session-expired).
    /// Auth/session endpoints go through SessionService's own client, so this
    /// handler never sees refresh calls and cannot recurse.
    /// </summary>
    public sealed class AuthHeaderHandler : DelegatingHandler
    {
        private static readonly HttpRequestOptionsKey<bool> RetriedKey = new("kaamlink.auth.retried");

        private readonly ISessionService _session;

        public AuthHeaderHandler(ISessionService session) => _session = session;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await AttachTokenAsync(request);

            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusCode.Unauthorized
                || (request.Options.TryGetValue(RetriedKey, out var alreadyRetried) && alreadyRetried))
                return response;

            if (!await _session.RefreshAsync())
                return response;

            response.Dispose();

            using var retry = await CloneAsync(request, cancellationToken);
            retry.Options.Set(RetriedKey, true);
            await AttachTokenAsync(retry);
            return await base.SendAsync(retry, cancellationToken);
        }

        private async Task AttachTokenAsync(HttpRequestMessage request)
        {
            var token = await _session.GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // HttpRequestMessage is single-send; our bodies are small JSON strings.
        private static async Task<HttpRequestMessage> CloneAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            if (request.Content is not null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                clone.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            return clone;
        }
    }
}
