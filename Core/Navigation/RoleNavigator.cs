using KaamLink.Core.Api;

namespace KaamLink.Core.Navigation
{
    /// <summary>
    /// The single place that decides where a role choice leads:
    /// authenticated + onboarded → home; authenticated, not onboarded → onboarding;
    /// unauthenticated → login first. The backend's /me summary drives these
    /// decisions — the app never guesses onboarding state locally.
    /// </summary>
    public static class RoleNavigator
    {
        public const string Worker = "worker";
        public const string Employer = "employer";

        /// <summary>Absolute route for the post-login destination of a role.</summary>
        public static string AbsoluteRouteFor(UserSummaryDto user, string role) =>
            AppRoutes.Absolute(RouteFor(user, role));

        public static string RouteFor(UserSummaryDto user, string role) => role switch
        {
            Worker => user.Worker.OnboardingComplete ? AppRoutes.WorkerHome : AppRoutes.WorkerOnboarding,
            Employer => user.Memberships.Count > 0 ? AppRoutes.EmployerHome : AppRoutes.EmployerOnboarding,
            _ => AppRoutes.Welcome
        };

        /// <summary>Login URI carrying the intended role through the OTP round-trip.</summary>
        public static string LoginRouteFor(string role) =>
            $"{AppRoutes.Absolute(AppRoutes.Login)}?role={role}";

        /// <summary>OTP URI carrying the verified phone + intended role.</summary>
        public static string OtpRouteFor(string phone, string role, int resendCooldownSeconds) =>
            $"{AppRoutes.Absolute(AppRoutes.Otp)}?phone={Uri.EscapeDataString(phone)}&role={role}&cooldown={resendCooldownSeconds}";
    }
}
