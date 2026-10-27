using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace KaamLink.Core.Api
{
    /// <summary>A verified Supabase session (only the access token is used by KaamLink).</summary>
    public sealed record SupabaseSession(string AccessToken, string? RefreshToken, int? ExpiresIn);

    /// <summary>Email OTP verification, password creation and password sign-in.</summary>
    public interface ISupabaseEmailAuthService
    {
        Task SendOtpAsync(string email, CancellationToken ct = default);

        Task<SupabaseSession> VerifyOtpAsync(string email, string token, CancellationToken ct = default);

        Task SetPasswordAsync(string accessToken, string password, CancellationToken ct = default);

        Task<SupabaseSession> SignInWithPasswordAsync(
            string email, string password, CancellationToken ct = default);
    }

    public sealed class SupabaseEmailAuthService : ISupabaseEmailAuthService
    {
        private readonly HttpClient _http;
        private readonly INetworkStatus _network;

        public SupabaseEmailAuthService(HttpClient http, INetworkStatus network)
        {
            _http = http;
            _network = network;
        }

        public Task SendOtpAsync(string email, CancellationToken ct = default) =>
            SendAsync<object?>(
                HttpMethod.Post,
                SupabaseConfig.OtpUri,
                new SupabaseOtpRequestDto(email.Trim().ToLowerInvariant(), true),
                AppJsonContext.Default.SupabaseOtpRequestDto,
                expected: null,
                errorCode: ApiClient.Codes.SupabaseSendFailed,
                bearerToken: null,
                ct: ct);

        public Task<SupabaseSession> VerifyOtpAsync(string email, string token, CancellationToken ct = default) =>
            SendAsync<SupabaseSession>(
                HttpMethod.Post,
                SupabaseConfig.VerifyUri,
                new SupabaseVerifyOtpRequestDto(email.Trim().ToLowerInvariant(), token, "email"),
                AppJsonContext.Default.SupabaseVerifyOtpRequestDto,
                expected: AppJsonContext.Default.SupabaseSessionResponseDto,
                errorCode: ApiClient.Codes.SupabaseOtpInvalid,
                bearerToken: null,
                ct: ct);

        public Task SetPasswordAsync(string accessToken, string password, CancellationToken ct = default) =>
            SendAsync<object?>(
                HttpMethod.Put,
                SupabaseConfig.UserUri,
                new SupabaseUpdateUserRequestDto(
                    password,
                    new SupabaseUserMetadataDto(true)),
                AppJsonContext.Default.SupabaseUpdateUserRequestDto,
                expected: null,
                errorCode: ApiClient.Codes.SupabasePasswordSetFailed,
                bearerToken: accessToken,
                ct: ct);

        public Task<SupabaseSession> SignInWithPasswordAsync(
            string email, string password, CancellationToken ct = default) =>
            SendAsync<SupabaseSession>(
                HttpMethod.Post,
                SupabaseConfig.TokenUri,
                new SupabasePasswordLoginRequestDto(email.Trim().ToLowerInvariant(), password),
                AppJsonContext.Default.SupabasePasswordLoginRequestDto,
                expected: AppJsonContext.Default.SupabaseSessionResponseDto,
                errorCode: ApiClient.Codes.SupabaseInvalidCredentials,
                bearerToken: null,
                ct: ct);

        private async Task<TResult> SendAsync<TResult>(
            HttpMethod method,
            Uri uri,
            object body,
            System.Text.Json.Serialization.Metadata.JsonTypeInfo bodyTypeInfo,
            System.Text.Json.Serialization.Metadata.JsonTypeInfo<SupabaseSessionResponseDto>? expected,
            string errorCode,
            string? bearerToken,
            CancellationToken ct)
        {
            if (!SupabaseConfig.IsConfigured)
                throw new ApiException(
                    SupabaseConfig.ConfigurationIssue ?? "Email sign-in is not configured.",
                    status: null,
                    ApiClient.Codes.SupabaseNotConfigured);

            if (!_network.HasUsableNetwork())
                throw ApiException.NoInternet();

            using var request = new HttpRequestMessage(method, uri);
            request.Headers.TryAddWithoutValidation("apikey", SupabaseConfig.AnonKey);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", string.IsNullOrWhiteSpace(bearerToken) ? SupabaseConfig.AnonKey : bearerToken);
            request.Content = JsonContent.Create(body, bodyTypeInfo);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct);
            }
            catch (HttpRequestException exception)
            {
                Console.WriteLine($"[KaamLink] Supabase email auth failed: {exception.Message}");
                throw ApiException.Offline(exception);
            }
            catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
            {
                Console.WriteLine($"[KaamLink] Supabase email auth timed out: {exception.Message}");
                throw ApiException.Offline(exception);
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    throw new ApiException("Too many attempts.", (int)response.StatusCode,
                        ApiClient.Codes.SupabaseRateLimited);

                if (!response.IsSuccessStatusCode)
                    throw new ApiException(
                        "Supabase rejected the request.",
                        (int)response.StatusCode,
                        errorCode);

                if (expected is null)
                    return default!;

                SupabaseSessionResponseDto? payload;
                try
                {
                    payload = await response.Content.ReadFromJsonAsync(expected, ct);
                }
                catch (System.Text.Json.JsonException exception)
                {
                    throw new ApiException(
                        "Supabase returned an unexpected response.",
                        (int)response.StatusCode,
                        errorCode,
                        exception);
                }

                if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
                    throw new ApiException(
                        "Supabase did not return an access token.",
                        (int)response.StatusCode,
                        errorCode);

                return (TResult)(object)new SupabaseSession(
                    payload.AccessToken, payload.RefreshToken, payload.ExpiresIn);
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
