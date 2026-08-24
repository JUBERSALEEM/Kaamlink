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

        /// <summary>Worker job feed (Phase 04).</summary>
        public const string VacancyFeed = "vacancyfeed";

        /// <summary>Vacancy detail + apply. Accepts ?vacancyId=.</summary>
        public const string VacancyDetail = "vacancydetail";

        /// <summary>The worker's own applications.</summary>
        public const string MyApplications = "myapplications";

        /// <summary>Employer vacancy list. Accepts ?companyId=&amp;companyName=.</summary>
        public const string CompanyVacancies = "companyvacancies";

        /// <summary>Create/edit a vacancy. Accepts ?companyId= and optional ?vacancyId=.</summary>
        public const string VacancyEdit = "vacancyedit";

        /// <summary>Applicants of one vacancy. Accepts ?companyId=&amp;vacancyId=&amp;title=.</summary>
        public const string VacancyApplications = "vacancyapplications";

        /// <summary>Worker's skill assessments (Phase 05).</summary>
        public const string MyAssessments = "myassessments";

        /// <summary>Assessment runner. Accepts ?skillId=&amp;skillName=.</summary>
        public const string AssessmentRun = "assessmentrun";

        /// <summary>Absolute Shell URI that replaces the navigation stack.</summary>
        public static string Absolute(string route) => "//" + route;
    }
}
