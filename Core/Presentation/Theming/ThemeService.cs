using KaamLink.Core.Presentation.Clay;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace KaamLink.Core.Presentation.Theming
{
    /// <summary>
    /// Swaps the palette <see cref="ResourceDictionary"/> merged into
    /// <see cref="Application.Resources"/> and re-applies clay shadows.
    ///
    /// The palette is a COMPILED XAML dictionary (ClayPalette / ClayDuskPalette).
    /// Assigning <c>new ResourceDictionary { Source = ... }</c> from C# does not
    /// actually load MAUI XAML, so the old implementation silently added an empty
    /// dictionary and the theme toggle appeared to do nothing.
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

            // All resource-dictionary mutation must happen on the UI thread.
            // Dispatcher is preferred over MainThread.IsMainThread: it is the same
            // dispatcher MAUI uses to marshal resource-change notifications.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null && dispatcher.IsDispatchRequired)
            {
                try
                {
                    dispatcher.Dispatch(() => SwapSafely(theme));
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[KaamLink] theme dispatch failed: {exception}");
                }
                return;
            }

            SwapSafely(theme);
        }

        public string Toggle()
        {
            var next = ThemeCatalog.Next(CurrentThemeId);
            Apply(next.Id);
            // Return the state that was actually applied, not the requested one.
            // If the palette load failed, the affordance must not lie.
            return CurrentThemeId;
        }

        private void SwapSafely(ThemeDefinition theme)
        {
            try
            {
                SwapCore(theme);
            }
            catch (Exception exception)
            {
                // Startup and UI-tap paths both use this. A failed palette load must
                // degrade to the currently merged palette, never stop the process.
                Console.WriteLine($"[KaamLink] theme '{theme.Id}' could not be applied: {exception}");
            }
        }

        private void SwapCore(ThemeDefinition theme)
        {
            var application = Application.Current;
            var merged = application?.Resources.MergedDictionaries;
            if (merged is null)
                return;

            var replacement = theme.CreatePalette();
            if (!replacement.ContainsKey(ThemeCatalog.PaletteMarkerKey))
                throw new InvalidOperationException(
                    $"Palette '{theme.Id}' is missing {ThemeCatalog.PaletteMarkerKey}; it is not a theme dictionary.");

            // MergedDictionaries is ICollection<ResourceDictionary> in .NET MAUI -
            // no indexer, Insert or RemoveAt. Remove then Add makes MAUI raise the
            // change that repaints DynamicResource consumers. Every catalogue
            // palette is removed, not just the tracked one, so the collection
            // cannot grow on every toggle.
            foreach (var dictionary in merged.ToList())
            {
                if (IsPalette(dictionary) || ReferenceEquals(dictionary, _palette))
                    merged.Remove(dictionary);
            }

            merged.Add(replacement);
            _palette = replacement;

            CurrentThemeId = theme.Id;
            WriteStoredThemeId(theme.Id);

            // Keep platform/native controls on the matching side too. This does not
            // replace the palette swap; it just aligns status bars, dialogs, etc.
            if (application is not null)
                application.UserAppTheme = theme.Id == ThemeCatalog.ClayDusk
                    ? AppTheme.Dark
                    : AppTheme.Light;

            ThemeChanged?.Invoke(this, theme.Id);

            // Always defer one dispatcher turn so the replacement dictionary is
            // fully loaded before shadows read ClayShadowBrush from it. Calling
            // RefreshClayShadows synchronously here was a real crash path.
            var dispatcher = application?.Dispatcher;
            if (dispatcher is not null)
                dispatcher.Dispatch(RefreshClayShadows);
            else
                RefreshClayShadows();
        }

        private static bool IsPalette(ResourceDictionary dictionary)
        {
            // Compiled dictionaries carry the marker; the initial App.xaml palette
            // is still Source-based, so keep the source check as a fallback.
            if (dictionary.TryGetValue(ThemeCatalog.PaletteMarkerKey, out var marker)
                && marker is string themeId
                && ThemeCatalog.IsKnown(themeId))
            {
                return true;
            }

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
            try
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
            catch (Exception exception)
            {
                // Runs on the dispatcher after a switch, so an escaping exception
                // would be an unhandled UI-thread crash. A missed shadow refresh is
                // cosmetic and must stay cosmetic.
                Console.WriteLine($"[KaamLink] clay shadow refresh failed: {exception}");
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
