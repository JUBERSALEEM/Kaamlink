using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace KaamLink.Core.Api
{
    /// <summary>
    /// The app's session: "who is signed in and what do we know about them".
    /// Owns the token pair (via <see cref="ITokenStore"/>), refresh rotation and the
    /// cached identity summary that drives role gating (worker progress, memberships).
    ///
    /// Uses its OWN plain HttpClient (no auth handler) for auth endpoints so the
    /// ApiClient → AuthHeaderHandler → SessionService graph can never cycle. That
    /// client comes from IHttpClientFactory and shares ApiHttp.Configure with the
    /// typed API client, so both use the same base URL and 15-second timeout.
    /// </summary>
    public interface ISessionService
    {
        bool IsAuthenticated { get; }

        /// <summary>Last known identity summary (drives navigation gating). Null when signed out.</summary>
        UserSummaryDto? CurrentUser { get; }

        /// <summary>Cold-start restore: rotates the stored refresh token and reloads the identity.</summary>
        Task RestoreAsync();

        /// <summary>Called after a successful OTP verify; persists the pair and adopts the identity.</summary>
        Task CompleteLoginAsync(AuthTokensResponseDto tokens);

        Task<string?> GetAccessTokenAsync();

        /// <summary>Rotates the refresh token single-flight. False = session is no longer valid (or offline).</summary>
        Task<bool> RefreshAsync();

        /// <summary>Reloads the identity summary (/me) after profile/company changes.</summary>
        Task<UserSummaryDto?> ReloadUserAsync();

        Task LogoutAsync();
    }

    public sealed partial class SessionService : ObservableObject, ISessionService
    {
        private readonly HttpClient _authHttp;
        private readonly INetworkStatus _network;
        private readonly ITokenStore _tokenStore;
        private readonly ILogger<SessionService> _logger;

        // Single-flight refresh: concurrent 401 handlers share one rotation.
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        [ObservableProperty]
        private bool _isAuthenticated;

        [ObservableProperty]
        private UserSummaryDto? _currentUser;

        public SessionService(
            IHttpClientFactory httpClientFactory,
            INetworkStatus network,
            ITokenStore tokenStore,
            ILogger<SessionService> logger)
        {
            _authHttp = httpClientFactory.CreateClient(ApiHttp.AuthClientName);
            _network = network;
            _tokenStore = tokenStore;
            _logger = logger;
        }

        /// <summary>
        /// A Release build without an endpoint (or a device with no network) must not
        /// make cold start wait for HttpClient.Timeout. ApiClient performs the same
        /// check for normal calls; session restore needs it too.
        /// </summary>
        private bool CanReachApi => ApiConfig.IsConfigured && _network.HasUsableNetwork();

        public async Task RestoreAsync()
        {
            if (await _tokenStore.GetRefreshTokenAsync() is null)
            {
                await ResetStateAsync();
                return;
            }

            if (!CanReachApi)
            {
                _logger.LogInformation(
                    "Session restore skipped: configured={Configured}, network={Network}, source={Source}",
                    ApiConfig.IsConfigured, _network.Describe(), ApiConfig.ConfigurationSource);
                return;
            }

            if (!await RefreshAsync())
                return; // RefreshAsync already reset state when the server rejected us.

            await ReloadUserAsync();
        }

        public async Task CompleteLoginAsync(AuthTokensResponseDto tokens)
        {
            await _tokenStore.SaveAsync(tokens.AccessToken, tokens.RefreshToken);
            CurrentUser = tokens.User;
            IsAuthenticated = true;
        }

        public Task<string?> GetAccessTokenAsync() => _tokenStore.GetAccessTokenAsync();

        public async Task<bool> RefreshAsync()
        {
            if (!CanReachApi)
                return false;

            await _refreshLock.WaitAsync();
            try
            {
                var refreshToken = await _tokenStore.GetRefreshTokenAsync();
                if (refreshToken is null)
                    return false;

                try
                {
                    using var response = await _authHttp.PostAsJsonAsync(
                        ApiConfig.ApiPrefix + "auth/token/refresh",
                        new RefreshTokenRequestDto(refreshToken),
                        AppJsonContext.Default.RefreshTokenRequestDto);

                    if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
                    {
                        // Token revoked/reused/expired — the session is genuinely over.
                        await ResetStateAsync();
                        return false;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Refresh failed with status {Status}", (int)response.StatusCode);
                        return false;
                    }

                    var tokens = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AuthTokensResponseDto);
                    if (tokens is null)
                        return false;

                    await _tokenStore.SaveAsync(tokens.AccessToken, tokens.RefreshToken);
                    CurrentUser = tokens.User;
                    IsAuthenticated = true;
                    return true;
                }
                catch (Exception exception) when (exception is HttpRequestException
                                                    or OperationCanceledException
                                                    or System.Text.Json.JsonException)
                {
                    // Offline/timeout/malformed 2xx body: keep the pair — the next
                    // app start will try again. A captive portal returning HTML
                    // used to escape as JsonException and crash the 401 retry path.
                    _logger.LogWarning(exception, "Refresh could not complete against the server");
                    return false;
                }
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        public async Task<UserSummaryDto?> ReloadUserAsync()
        {
            var accessToken = await _tokenStore.GetAccessTokenAsync();
            if (accessToken is null)
                return null;
            if (!CanReachApi)
                return CurrentUser;

            using var request = new HttpRequestMessage(HttpMethod.Get, ApiConfig.ApiPrefix + "auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            try
            {
                using var response = await _authHttp.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return CurrentUser;

                var user = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.UserSummaryDto);
                if (user is not null)
                {
                    CurrentUser = user;
                    IsAuthenticated = true;
                }

                return user;
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
            {
                // Offline/timeout: keep the last known summary rather than crashing.
                _logger.LogWarning(exception, "Identity reload could not reach the server");
                return CurrentUser;
            }
            catch (System.Text.Json.JsonException exception)
            {
                // A captive portal/proxy can answer 200 with HTML; keep the cached identity.
                _logger.LogWarning(exception, "Identity reload returned an unexpected body");
                return CurrentUser;
            }
        }

        public async Task LogoutAsync()
        {
            var refreshToken = await _tokenStore.GetRefreshTokenAsync();
            var accessToken = await _tokenStore.GetAccessTokenAsync();

            if (refreshToken is not null && accessToken is not null && CanReachApi)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, ApiConfig.ApiPrefix + "auth/logout")
                    {
                        Content = JsonContent.Create(
                            new LogoutRequestDto(refreshToken),
                            AppJsonContext.Default.LogoutRequestDto)
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    using var response = await _authHttp.SendAsync(request);
                }
                catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
                {
                    // Best effort — local state is cleared either way.
                    _logger.LogWarning(exception, "Logout could not reach the server");
                }
            }

            await ResetStateAsync();
        }

        private async Task ResetStateAsync()
        {
            await _tokenStore.ClearAsync();
            CurrentUser = null;
            IsAuthenticated = false;
        }
    }
}
