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

        // The palette dictionary this service merged, so a source path that stops
        // matching cannot hide it from the removal sweep.
        private ResourceDictionary? _palette;

        public ThemeService() => CurrentThemeId = ReadStoredThemeId();

        public string CurrentThemeId { get; private set; }

        public event EventHandler<string>? ThemeChanged;

        public void Apply(string themeId)
        {
            var theme = ThemeCatalog.Resolve(themeId);

            void Swap()
            {
                // MergedDictionaries is ICollection<ResourceDictionary> in .NET MAUI -
                // no indexer, no Insert, no RemoveAt. So the replacement is appended
                // rather than placed back in the palette's slot. That is safe only
                // because no key is defined by more than one merged dictionary
                // (asserted by tools/verify-ui-redesign.py section K), so merge ORDER
                // cannot change which value a key resolves to.
                var merged = Application.Current?.Resources.MergedDictionaries;
                if (merged is null)
                    return;

                var replacement = new ResourceDictionary
                {
                    Source = new Uri(theme.PaletteSource, UriKind.Relative)
                };

                // Remove then Add (rather than mutating in place) is what makes MAUI
                // raise the change that repaints DynamicResource consumers. Every
                // catalogue palette is removed, not just the tracked one: if a source
                // path ever stops matching, a single lookup would leave the old palette
                // merged and the collection would grow on every toggle.
                foreach (var dictionary in merged.ToList())
                {
                    if (IsPalette(dictionary) || ReferenceEquals(dictionary, _palette))
                        merged.Remove(dictionary);
                }

                merged.Add(replacement);
                _palette = replacement;

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
