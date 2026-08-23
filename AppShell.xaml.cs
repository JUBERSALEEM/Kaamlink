using KaamLink.Features.Employer;
using KaamLink.Features.Worker;

namespace KaamLink
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Worker and Employer experience roots. Pushed on top of the role-selection
            // page for now; replaced by role-gated navigation once auth exists (Phase 02+).
            Routing.RegisterRoute(AppRoutes.WorkerHome, typeof(WorkerHomePage));
            Routing.RegisterRoute(AppRoutes.EmployerHome, typeof(EmployerHomePage));
        }
    }
}
