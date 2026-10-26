using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace KaamLink
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
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
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
