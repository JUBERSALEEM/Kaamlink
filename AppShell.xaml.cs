namespace KaamLink
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // All routes are top-level ShellContent items in AppShell.xaml —
            // absolute navigation (AppRoutes.Absolute) is the app's only navigation
            // mode, so no dynamic route registration is needed here.
        }
    }
}
