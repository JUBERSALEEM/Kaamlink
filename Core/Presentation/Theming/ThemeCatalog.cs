namespace KaamLink.Core.Presentation.Theming
{
    /// <summary>
    /// One selectable theme: an id (stable, persisted), the XAML source (verified
    /// by the static gates) and a compiled-dictionary factory.
    ///
    /// The factory is important: .NET MAUI does not load a ResourceDictionary from
    /// a relative Source assigned in C#. Runtime theme switches must instantiate
    /// the XAML-generated dictionary class instead.
    /// </summary>
    public sealed record ThemeDefinition(
        string Id,
        string PaletteSource,
        Func<ResourceDictionary> CreatePalette);

    /// <summary>The themes the app ships with, in the order the toggle cycles them.</summary>
    public static class ThemeCatalog
    {
        public const string Clay = "clay";
        public const string ClayDusk = "clay-dusk";

        /// <summary>Key present in every palette so ThemeService can identify it without relying on Source.</summary>
        public const string PaletteMarkerKey = "KaamLinkThemeId";

        public static readonly IReadOnlyList<ThemeDefinition> All =
        [
            new(Clay, "Resources/Styles/Colors.xaml",
                () => new KaamLink.Resources.Styles.ClayPalette()),
            new(ClayDusk, "Resources/Themes/ClayDusk.xaml",
                () => new KaamLink.Resources.Themes.ClayDuskPalette())
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
