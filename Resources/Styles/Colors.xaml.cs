namespace KaamLink.Resources.Styles;

/// <summary>
/// Compiled default palette. ThemeService instantiates this type directly at
/// runtime: a C#-created ResourceDictionary with only Source set does not load
/// XAML on .NET MAUI, which made the theme toggle appear to do nothing.
/// </summary>
public partial class ClayPalette : ResourceDictionary
{
    public ClayPalette() => InitializeComponent();
}
