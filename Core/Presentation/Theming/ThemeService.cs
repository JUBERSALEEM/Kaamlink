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

                // Remove + insert (rather than assigning into the slot) is what makes
                // MAUI raise the change that repaints DynamicResource consumers.
                // Every catalogue palette is removed, not just the active one: if a
                // source path ever stops matching (renamed file, leading slash), a
                // single-palette lookup would leave a duplicate merged and the list
                // would grow on every toggle.
                var index = RemovePalettes(merged);
                merged.Insert(index < 0 ? 0 : index, replacement);

                CurrentThemeId = theme.Id;
                WriteStoredThemeId(theme.Id);
                ThemeChanged?.Invoke(this, theme.Id);

                // Deferred by one dispatcher turn so the replacement dictionary is
                // loaded before shadows read ClayShadowBrush from it; resolving too
                // early would give every shadow the fallback colour.
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is not null && !dispatcher.IsDispatchRequired)
                    dispatcher.Dispatch(RefreshClayShadows);
                else
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

        /// <summary>
        /// Removes every merged dictionary that is a catalogue palette and returns
        /// the lowest index it occupied (-1 if none) so the caller can re-insert
        /// there and keep the merge order (icons, palette, styles) intact.
        /// </summary>
        private static int RemovePalettes(IList<ResourceDictionary> merged)
        {
            var lowest = -1;
            for (var index = merged.Count - 1; index >= 0; index--)
            {
                if (!IsPalette(merged[index]))
                    continue;

                merged.RemoveAt(index);
                lowest = index;
            }

            return lowest;
        }

        private static bool IsPalette(ResourceDictionary dictionary)
        {
            var source = Normalize(dictionary.Source);
            return source is not null
                && ThemeCatalog.All.Any(theme =>
                    string.Equals(Normalize(theme.PaletteSource), source, StringComparison.OrdinalIgnoreCase));
        }

        private static string? Normalize(Uri? uri) => Normalize(uri?.OriginalString);

        private static string? Normalize(string? source) =>
            source?.TrimStart('/').Replace('\\', '/');

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
