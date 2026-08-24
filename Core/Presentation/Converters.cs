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
                _ => "TextSecondary"
            };

            return Application.Current?.Resources.TryGetValue(tokenKey, out var color) == true
                ? color
                : Colors.Gray;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
