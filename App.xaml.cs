using KaamLink.Core.Presentation.Theming;

namespace KaamLink
{
    public partial class App : Application
    {
        private readonly IThemeService _theme;

        /// <summary>
        /// The theme service is injected (not resolved statically) and applies the
        /// stored palette before the first page is created, so the app never
        /// flashes the default colours on a dark-theme start.
        /// </summary>
        public App(IThemeService theme)
        {
            InitializeComponent();
            _theme = theme;
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
