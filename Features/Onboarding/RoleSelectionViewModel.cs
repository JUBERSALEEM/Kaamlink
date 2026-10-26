using KaamLink.Core.Api;
using KaamLink.Core.Presentation.Theming;
using KaamLink.Resources.Strings;

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
        private readonly IThemeService _theme;
        private bool _sessionRestored;

        public RoleSelectionViewModel(ISessionService session, IThemeService theme)
        {
            _session = session;
            _theme = theme;
            SyncThemeAffordance();
        }

        /// <summary>
        /// Label for the theme control. It names the mode a tap switches TO (the
        /// action), not the current mode, so the control never reads as a status
        /// badge that does nothing.
        /// </summary>
        [ObservableProperty]
        private string _themeToggleLabel = AppStrings.ThemeSwitchToDusk;

        /// <summary>True when the dusk palette is active (drives the sun/moon swap).</summary>
        [ObservableProperty]
        private bool _isDusk;

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
        private void ToggleTheme()
        {
            _theme.Toggle();
            SyncThemeAffordance();
        }

        private void SyncThemeAffordance()
        {
            var dusk = _theme.CurrentThemeId == ThemeCatalog.ClayDusk;
            IsDusk = dusk;
            ThemeToggleLabel = dusk ? AppStrings.ThemeSwitchToLight : AppStrings.ThemeSwitchToDusk;
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
