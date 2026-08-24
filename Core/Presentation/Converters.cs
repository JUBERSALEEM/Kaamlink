using System.Globalization;

namespace KaamLink.Core.Presentation
{
    /// <summary>
    /// Maps a company verification-state enum name (Pending/Verified/Rejected —
    /// API contract values) to a design-token color. Token keys live in
    /// Resources/Styles/Colors.xaml; colors are looked up at runtime, never hard-coded here.
    /// </summary>
    public sealed class VerificationStateColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var tokenKey = value as string switch
            {
                "Pending" => "Warning",
                "Verified" => "Success",
                "Rejected" => "Error",
                _ => "TextSecondary"
            };

            return Application.Current?.Resources.TryGetValue(tokenKey, out var color) == true
                ? color
                : Colors.Gray;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Maps Phase-04 lifecycle enum names (VacancyStatus / ApplicationStatus —
    /// API contract values) to a design-token color for status pills.
    /// </summary>
    public sealed class StatusColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var tokenKey = value as string switch
            {
                "Draft" => "TextSecondary",
                "Active" => "Success",
                "Paused" => "Warning",
                "Closed" => "Error",
                "Applied" => "Primary",
                "Viewed" => "TextSecondary",
                "Shortlisted" => "Success",
                "Rejected" => "Error",
                "Hired" => "Success",
                "Withdrawn" => "TextSecondary",
                "SelfDeclared" => "TextSecondary",
                "EmployerVerified" => "Success",
                "AssessmentVerified" => "Success",
                _ => "TextSecondary"
            };

            return Application.Current?.Resources.TryGetValue(tokenKey, out var color) == true
                ? color
                : Colors.Gray;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Boolean inverter for IsEnabled/IsVisible bindings. MAUI core ships no built-in
    /// inverter (the familiar InvertedBoolConverter lives in the CommunityToolkit.Maui
    /// package); this 3-line one keeps us at zero extra dependencies.
    /// Registered in App.xaml under the conventional key "InvertedBoolConverter".
    /// </summary>
    public sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is not true;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is not true;
    }

    /// <summary>Selected assessment option → filled brand background (false → card surface).</summary>
    public sealed class SelectedOptionBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            Lookup(value is true ? "Primary" : "CardSurface");

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        private static object? Lookup(string key) =>
            Application.Current?.Resources.TryGetValue(key, out var color) == true ? color : Colors.Gray;
    }

    /// <summary>Selected assessment option → on-brand text color (false → primary text).</summary>
    public sealed class SelectedOptionTextColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            Lookup(value is true ? "TextOnPrimary" : "TextPrimary");

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        private static object? Lookup(string key) =>
            Application.Current?.Resources.TryGetValue(key, out var color) == true ? color : Colors.Gray;
    }
}
