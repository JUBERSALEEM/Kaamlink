namespace KaamLink.Core.Navigation
{
    /// <summary>
    /// Central registry of Shell route names. Route strings never appear as
    /// magic strings elsewhere — add new routes here when adding pages.
    /// Values must match the Route attributes in AppShell.xaml.
    /// All app navigation is ABSOLUTE ("//route"): the auth/onboarding flow must
    /// never leave login pages behind on the navigation stack.
    /// </summary>
    public static class AppRoutes
    {
        /// <summary>Role-selection entry page.</summary>
        public const string Welcome = "welcome";

        /// <summary>Phone-number sign-in. Accepts ?role=worker|employer.</summary>
        public const string Login = "login";

        /// <summary>OTP verification. Accepts ?phone= and ?role=.</summary>
        public const string Otp = "otp";

        /// <summary>Worker 3-step onboarding (basics → skills → preferences).</summary>
        public const string WorkerOnboarding = "worker-onboarding";

        /// <summary>Worker experience root.</summary>
        public const string WorkerHome = "workerhome";

        /// <summary>Employer company registration.</summary>
        public const string EmployerOnboarding = "employer-onboarding";

        /// <summary>Employer experience root.</summary>
        public const string EmployerHome = "employerhome";

        /// <summary>Absolute Shell URI that replaces the navigation stack.</summary>
        public static string Absolute(string route) => "//" + route;
    }
}
