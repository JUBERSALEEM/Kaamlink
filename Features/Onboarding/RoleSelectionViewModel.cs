using KaamLink.Core.Api;

namespace KaamLink.Features.Onboarding
{
    /// <summary>
    /// Entry-point choice between the Worker and Employer experiences.
    /// Session-aware: a returning user with a valid session goes straight to
    /// their destination; anyone else signs in first. The backend's /me summary
    /// (RoleNavigator) decides home vs onboarding — never local flags.
    /// </summary>
    public partial class RoleSelectionViewModel : ObservableObject
    {
        private readonly ISessionService _session;
        private bool _sessionRestored;

        public RoleSelectionViewModel(ISessionService session) => _session = session;

        [ObservableProperty]
        private bool _isBusy;

        /// <summary>Called once from the page (appearing) to restore a stored session.</summary>
        public async Task EnsureSessionRestoredAsync()
        {
            if (_sessionRestored)
                return;
            _sessionRestored = true;

            IsBusy = true;
            try
            {
                await _session.RestoreAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private Task NavigateToWorkerAsync() => NavigateWithRoleAsync(RoleNavigator.Worker);

        [RelayCommand]
        private Task NavigateToEmployerAsync() => NavigateWithRoleAsync(RoleNavigator.Employer);

        private async Task NavigateWithRoleAsync(string role)
        {
            if (_session.IsAuthenticated && _session.CurrentUser is not null)
            {
                await Shell.Current.GoToAsync(RoleNavigator.AbsoluteRouteFor(_session.CurrentUser, role));
                return;
            }

            await Shell.Current.GoToAsync(RoleNavigator.LoginRouteFor(role));
        }
    }
}
