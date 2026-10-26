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
            Lookup(value is true ? "Primary" : "TextSecondary");

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        private static object? Lookup(string key) =>
            Application.Current?.Resources.TryGetValue(key, out var color) == true ? color : Colors.Gray;
    }
    /// <summary>
    /// Companion to <see cref="StatusColorConverter"/>: maps the same lifecycle
    /// enum names to the SOFT (tinted) background token used behind status pills.
    /// Keeping tone decisions here means a pill can never be recolored ad hoc in
    /// a page, and the API enum names stay the single source of meaning.
    /// </summary>
    public sealed class StatusSoftColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var tokenKey = value as string switch
            {
                "Draft" => "CardSurfaceAlt",
                "Active" => "SuccessSoft",
                "Paused" => "WarningSoft",
                "Closed" => "ErrorSoft",
                "Applied" => "PrimarySoft",
                "Viewed" => "CardSurfaceAlt",
                "Shortlisted" => "SuccessSoft",
                "Rejected" => "ErrorSoft",
                "Hired" => "SuccessSoft",
                "Withdrawn" => "CardSurfaceAlt",
                "SelfDeclared" => "CardSurfaceAlt",
                "EmployerVerified" => "SuccessSoft",
                "AssessmentVerified" => "SuccessSoft",
                "AssessmentPassed" => "SuccessSoft",
                "AssessmentFailed" => "ErrorSoft",
                _ => "CardSurfaceAlt"
            };

            return Application.Current?.Resources.TryGetValue(tokenKey, out var color) == true
                ? color
                : Colors.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Companion to <see cref="VerificationStateColorConverter"/>: company
    /// verification tone → soft pill background.
    /// </summary>
    public sealed class VerificationStateSoftColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var tokenKey = value as string switch
            {
                "Pending" => "WarningSoft",
                "Verified" => "SuccessSoft",
                "Rejected" => "ErrorSoft",
                _ => "CardSurfaceAlt"
            };

            return Application.Current?.Resources.TryGetValue(tokenKey, out var color) == true
                ? color
                : Colors.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// True when the bound string has content. Used to show/hide optional blocks
    /// (about text, cover notes) without adding extra boolean view-model members.
    /// </summary>
    public sealed class StringHasContentConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is string text && !string.IsNullOrWhiteSpace(text);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Count → "N items" style boolean used by small count badges.</summary>
    public sealed class CountPositiveConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is int count && count > 0;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
    /// <summary>
    /// Step-rail colouring for the onboarding stepper. The view model already
    /// exposes which step is active (IsStep1/IsStep2/IsStep3); this maps that to
    /// Primary (reached) or Divider (not reached) so the rail needs no extra state.
    /// ConverterParameter: "reached" = true means Primary, "ahead" = true means Divider.
    /// </summary>
    public sealed class StageColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var flag = value is true;
            var wantPrimary = parameter as string == "reached" ? flag : !flag;
            var tokenKey = wantPrimary ? "Primary" : "Divider";

            return Application.Current?.Resources.TryGetValue(tokenKey, out var color) == true
                ? color
                : Colors.Gray;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
    /// <summary>True when the bound count is zero (used for "nothing yet" hints).</summary>
    public sealed class CountEmptyConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is int count && count == 0;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
    /// <summary>
    /// True when the bound string equals the ConverterParameter (ordinal).
    /// Lets a page branch on a single API enum value without adding booleans to
    /// the view model (e.g. verification state == "Verified").
    /// </summary>
    public sealed class StringEqualsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is string text
            && parameter is string expected
            && string.Equals(text, expected, StringComparison.Ordinal);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
