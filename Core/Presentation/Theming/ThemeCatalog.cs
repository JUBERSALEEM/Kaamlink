namespace KaamLink.Core.Presentation.Theming
{
    /// <summary>
    /// One selectable theme: an id (stable, persisted) and the palette dictionary
    /// that supplies every colour token for it.
    ///
    /// A theme is a PALETTE SWAP. Layout, typography, spacing and control recipes
    /// all live in Styles.xaml and are shared by every theme, which is what keeps
    /// a new theme from becoming a second design.
    /// </summary>
    public sealed record ThemeDefinition(string Id, string PaletteSource);

    /// <summary>The themes the app ships with, in the order the toggle cycles them.</summary>
    public static class ThemeCatalog
    {
        public const string Clay = "clay";
        public const string ClayDusk = "clay-dusk";

        public static readonly IReadOnlyList<ThemeDefinition> All =
        [
            new(Clay, "Resources/Styles/Colors.xaml"),
            new(ClayDusk, "Resources/Themes/ClayDusk.xaml")
        ];

        public static ThemeDefinition Resolve(string? id) =>
            All.FirstOrDefault(theme => theme.Id == id) ?? All[0];

        public static ThemeDefinition Next(string? id)
        {
            var index = All.ToList().FindIndex(theme => theme.Id == Resolve(id).Id);
            return All[(index + 1) % All.Count];
        }

        public static bool IsKnown(string? id) => All.Any(theme => theme.Id == id);
    }

    /// <summary>Applies and reports the app-wide theme.</summary>
    public interface IThemeService
    {
        /// <summary>Id of the active theme; survives restarts.</summary>
        string CurrentThemeId { get; }

        /// <summary>Raised after a switch, with the new theme id.</summary>
        event EventHandler<string>? ThemeChanged;

        /// <summary>Activates a theme. Unknown ids fall back to the default.</summary>
        void Apply(string themeId);

        /// <summary>Activates the next theme in the catalogue.</summary>
        string Toggle();
    }
}
