using KaamLink.Core.Api;
using KaamLink.Core.Presentation.Theming;
using KaamLink.Features.Auth;

namespace KaamLink
{
    public partial class App : Application
    {
        private readonly IThemeService _theme;
        private readonly IEmailAuthLinkService _emailAuthLinks;

        /// <summary>
        /// The theme service is injected (not resolved statically) and applies the
        /// stored palette before the first page is created, so the app never
        /// flashes the default colours on a dark-theme start. The email-link
        /// service survives a cold start from a magic-link deep link.
        /// </summary>
        public App(IThemeService theme, IEmailAuthLinkService emailAuthLinks)
        {
            InitializeComponent();
            _theme = theme;
            _emailAuthLinks = emailAuthLinks;
        }

        protected override void OnAppLinkRequestReceived(Uri uri)
        {
            base.OnAppLinkRequestReceived(uri);
            _emailAuthLinks.Store(uri);
            if (!EmailAuthLinkParser.IsCallback(uri))
                return;

            // Bring the sign-in page forward; it consumes the stored callback in
            // OnAppearing. A cold start may have no Shell yet — storing above is
            // enough in that case.
            _ = BringLoginToFrontAsync();
        }

        private static async Task BringLoginToFrontAsync()
        {
            try
            {
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (Shell.Current is null)
                        return;

                    await Shell.Current.GoToAsync("//login");
                    if (Shell.Current.CurrentPage is LoginPage loginPage)
                        await loginPage.HandleEmailAuthCallbackAsync();
                });
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KaamLink] email link navigation failed: {exception}");
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            ApplyStoredTheme();
            return new Window(new AppShell());
        }

        /// <summary>
        /// The theme is cosmetic; the palette merged by App.xaml is always a complete,
        /// valid theme. If applying a stored one fails, the app must still start with
        /// the default palette rather than crash before it has a window.
        /// </summary>
        private void ApplyStoredTheme()
        {
            try
            {
                _theme.Apply(_theme.CurrentThemeId);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KaamLink] theme apply failed, using default palette: {exception}");
            }
        }
    }
}
