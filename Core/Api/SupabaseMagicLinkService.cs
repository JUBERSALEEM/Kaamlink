using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace KaamLink.Core.Api
{
    /// <summary>Requests a passwordless email magic link from Supabase GoTrue.</summary>
    public interface ISupabaseMagicLinkService
    {
        Task SendLinkAsync(string email, CancellationToken ct = default);
    }

    public sealed class SupabaseMagicLinkService : ISupabaseMagicLinkService
    {
        private readonly HttpClient _http;
        private readonly INetworkStatus _network;

        public SupabaseMagicLinkService(HttpClient http, INetworkStatus network)
        {
            _http = http;
            _network = network;
        }

        public async Task SendLinkAsync(string email, CancellationToken ct = default)
        {
            var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (!IsValidEmail(normalized))
                throw new ApiException("Enter a valid email address.", status: null,
                    ApiClient.Codes.EmailInvalid);

            if (!SupabaseConfig.IsConfigured)
                throw new ApiException(
                    SupabaseConfig.ConfigurationIssue ?? "Email sign-in is not configured.",
                    status: null,
                    ApiClient.Codes.SupabaseNotConfigured);

            if (!_network.HasUsableNetwork())
                throw ApiException.NoInternet();

            using var request = new HttpRequestMessage(HttpMethod.Post, SupabaseConfig.OtpUri);
            request.Headers.TryAddWithoutValidation("apikey", SupabaseConfig.AnonKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SupabaseConfig.AnonKey);
            request.Content = JsonContent.Create(
                new SupabaseOtpRequestDto(normalized, true, SupabaseConfig.RedirectUri),
                AppJsonContext.Default.SupabaseOtpRequestDto);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct);
            }
            catch (HttpRequestException exception)
            {
                Console.WriteLine($"[KaamLink] Supabase magic-link send failed: {exception.Message}");
                throw ApiException.Offline(exception);
            }
            catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
            {
                Console.WriteLine($"[KaamLink] Supabase magic-link send timed out: {exception.Message}");
                throw ApiException.Offline(exception);
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    throw new ApiException("Too many sign-in emails.", (int)response.StatusCode,
                        ApiClient.Codes.SupabaseRateLimited);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[KaamLink] Supabase magic-link send returned {(int)response.StatusCode}");
                    throw new ApiException("We could not send the sign-in email.", (int)response.StatusCode,
                        ApiClient.Codes.SupabaseSendFailed);
                }
            }
        }

        /// <summary>Deliberately small client-side check; GoTrue remains authoritative.</summary>
        internal static bool IsValidEmail(string email)
        {
            if (email.Length is < 6 or > 320 || email.Contains(' '))
                return false;

            var at = email.IndexOf('@');
            if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
                return false;

            var domain = email[(at + 1)..];
            return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.');
        }
    }
}
