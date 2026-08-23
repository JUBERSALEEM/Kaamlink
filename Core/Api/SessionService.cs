using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace KaamLink.Core.Api
{
    /// <summary>
    /// The app's session: "who is signed in and what do we know about them".
    /// Owns the token pair (via <see cref="ITokenStore"/>), refresh rotation and the
    /// cached identity summary that drives role gating (worker progress, memberships).
    ///
    /// Uses its OWN plain HttpClient (no auth handler) for auth endpoints so the
    /// ApiClient → AuthHeaderHandler → SessionService graph can never cycle.
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
        // One shared socket for auth calls; the session singleton lives for the app lifetime.
        private static readonly HttpClient AuthHttp = new() { BaseAddress = ApiConfig.BaseUri };

        private readonly ITokenStore _tokenStore;
        private readonly ILogger<SessionService> _logger;

        // Single-flight refresh: concurrent 401 handlers share one rotation.
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        [ObservableProperty]
        private bool _isAuthenticated;

        [ObservableProperty]
        private UserSummaryDto? _currentUser;

        public SessionService(ITokenStore tokenStore, ILogger<SessionService> logger)
        {
            _tokenStore = tokenStore;
            _logger = logger;
        }

        public async Task RestoreAsync()
        {
            if (await _tokenStore.GetRefreshTokenAsync() is null)
            {
                await ResetStateAsync();
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
            await _refreshLock.WaitAsync();
            try
            {
                var refreshToken = await _tokenStore.GetRefreshTokenAsync();
                if (refreshToken is null)
                    return false;

                HttpResponseMessage response;
                try
                {
                    response = await AuthHttp.PostAsJsonAsync(
                        ApiConfig.ApiPrefix + "auth/token/refresh",
                        new RefreshTokenRequestDto(refreshToken),
                        AppJsonContext.Default.RefreshTokenRequestDto);
                }
                catch (HttpRequestException)
                {
                    // Offline: keep the pair — the next app start will try again.
                    return false;
                }

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

            using var request = new HttpRequestMessage(HttpMethod.Get, ApiConfig.ApiPrefix + "auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response;
            try
            {
                response = await AuthHttp.SendAsync(request);
            }
            catch (HttpRequestException)
            {
                return CurrentUser; // offline: keep the last known summary
            }

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

        public async Task LogoutAsync()
        {
            var refreshToken = await _tokenStore.GetRefreshTokenAsync();
            var accessToken = await _tokenStore.GetAccessTokenAsync();

            if (refreshToken is not null && accessToken is not null)
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
                    using var _ = await AuthHttp.SendAsync(request);
                }
                catch (HttpRequestException)
                {
                    // Best effort — local state is cleared either way.
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
