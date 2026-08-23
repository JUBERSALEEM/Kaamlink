namespace KaamLink.Core.Navigation
{
    /// <summary>
    /// Central registry of Shell route names. Route strings never appear as
    /// magic strings elsewhere — add new routes here when adding pages.
    /// Values must match the Route attributes in AppShell.xaml and the
    /// Routing.RegisterRoute calls in AppShell.xaml.cs.
    /// </summary>
    public static class AppRoutes
    {
        /// <summary>Role-selection entry page. Registered as a ShellContent in AppShell.xaml.</summary>
        public const string Welcome = "welcome";

        /// <summary>Worker experience root. Registered in AppShell.xaml.cs.</summary>
        public const string WorkerHome = "workerhome";

        /// <summary>Employer experience root. Registered in AppShell.xaml.cs.</summary>
        public const string EmployerHome = "employerhome";
    }
}
