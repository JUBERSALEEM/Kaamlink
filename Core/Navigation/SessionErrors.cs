using KaamLink.Core.Api;

namespace KaamLink.Core.Navigation
{
    /// <summary>
    /// Shared handling for "the server says this session is over" (401 that even a
    /// silent refresh could not recover — the refresh token was rejected and the
    /// token store was already cleared by the session service). All flows route the
    /// same way so the app never limps on with a dead session.
    /// </summary>
    public static class SessionErrors
    {
        public static bool IsExpiredSession(ApiException exception) =>
            exception.Status == 401;

        /// <summary>Expired sessions return directly to the app's sign-in entry.</summary>
        public static Task RouteToLoginAsync() =>
            Shell.Current.GoToAsync(AppRoutes.Absolute(AppRoutes.Login));
    }
}
