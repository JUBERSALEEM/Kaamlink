using KaamLink.Core.Presentation.Clay;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace KaamLink.Core.Presentation.Theming
{
    /// <summary>
    /// Swaps the palette <see cref="ResourceDictionary"/> merged into
    /// <see cref="Application.Resources"/> and re-applies clay shadows.
    ///
    /// Everything else follows automatically because pages and styles reference
    /// colour tokens with <c>{DynamicResource}</c>: replacing the palette
    /// dictionary raises a resource change, and every element repaints.
    /// </summary>
    public sealed class ThemeService : IThemeService
    {
        private const string PreferenceKey = "kaamlink.theme";

        public ThemeService() => CurrentThemeId = ReadStoredThemeId();

        public string CurrentThemeId { get; private set; }

        public event EventHandler<string>? ThemeChanged;

        public void Apply(string themeId)
        {
            var theme = ThemeCatalog.Resolve(themeId);

            void Swap()
            {
                var merged = Application.Current?.Resources.MergedDictionaries;
                if (merged is null)
                    return;

                var replacement = new ResourceDictionary
                {
                    Source = new Uri(theme.PaletteSource, UriKind.Relative)
                };

                var index = IndexOfPalette(merged);
                if (index >= 0)
                {
                    // Remove + insert (rather than replacing the entry) is what makes
                    // MAUI raise the resource change that repaints DynamicResource
                    // consumers; assigning into the slot can leave the UI stale.
                    merged.RemoveAt(index);
                    merged.Insert(index, replacement);
                }
                else
                {
                    // No palette merged yet (first run before App.xaml loads it).
                    merged.Insert(0, replacement);
                }

                CurrentThemeId = theme.Id;
                WriteStoredThemeId(theme.Id);
                ThemeChanged?.Invoke(this, theme.Id);
                RefreshClayShadows();
            }

            if (MainThread.IsMainThread)
                Swap();
            else
                MainThread.BeginInvokeOnMainThread(Swap);
        }

        public string Toggle()
        {
            var next = ThemeCatalog.Next(CurrentThemeId);
            Apply(next.Id);
            return next.Id;
        }

        private static int IndexOfPalette(IList<ResourceDictionary> merged)
        {
            for (var index = 0; index < merged.Count; index++)
            {
                var source = merged[index].Source?.OriginalString;
                if (source is not null && ThemeCatalog.All.Any(theme => theme.PaletteSource == source))
                    return index;
            }

            return -1;
        }

        private static void RefreshClayShadows()
        {
            var application = Application.Current;
            if (application is null)
                return;

            foreach (var window in application.Windows)
            {
                if (window.Page is { } page)
                    ClayMaterial.Refresh(page);
            }
        }

        // Preferences touches platform storage; a failure there must not stop the
        // app from starting with the default theme.
        private static string ReadStoredThemeId()
        {
            try
            {
                var stored = Preferences.Default.Get(PreferenceKey, ThemeCatalog.Clay);
                return ThemeCatalog.IsKnown(stored) ? stored : ThemeCatalog.Clay;
            }
            catch
            {
                return ThemeCatalog.Clay;
            }
        }

        private static void WriteStoredThemeId(string themeId)
        {
            try
            {
                Preferences.Default.Set(PreferenceKey, themeId);
            }
            catch
            {
                // Not fatal: the theme still applies for this session.
            }
        }
    }
}
