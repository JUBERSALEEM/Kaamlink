using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace KaamLink
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        Exported = true,
        LaunchMode = LaunchMode.SingleTop,
        // Resize (not pan) when the keyboard opens: form pages scroll, and the
        // sticky action dock stays visible above the IME instead of being hidden.
        WindowSoftInputMode = SoftInput.AdjustResize,
        ConfigurationChanges = ConfigChanges.ScreenSize
            | ConfigChanges.Orientation
            | ConfigChanges.UiMode
            | ConfigChanges.ScreenLayout
            | ConfigChanges.SmallestScreenSize
            | ConfigChanges.Density)]
    // Supabase email magic-link callback. The redirect is allowlisted in the
    // Supabase Auth settings; SingleTop ensures the existing activity receives it.
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "kaamlink",
        DataHost = "auth-callback")]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
