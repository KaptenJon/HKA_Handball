using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;
using AndroidX.Core.View;

namespace HKA_Handball;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Enable edge-to-edge for backward compatibility with Android 15+ (SDK 35)
        EdgeToEdge.Enable(this);

        base.OnCreate(savedInstanceState);

        if (Window is null)
            return;

        // Allow the app to draw into display cutout areas (notches)
        if (OperatingSystem.IsAndroidVersionAtLeast(30) && Window.Attributes is not null)
        {
            Window.Attributes.LayoutInDisplayCutoutMode =
                Android.Views.LayoutInDisplayCutoutMode.Always;
        }
        else if (OperatingSystem.IsAndroidVersionAtLeast(28) && Window.Attributes is not null)
        {
            Window.Attributes.LayoutInDisplayCutoutMode =
                Android.Views.LayoutInDisplayCutoutMode.ShortEdges;
        }

        // Hide system bars for immersive fullscreen game experience
        var insetsController = WindowCompat.GetInsetsController(Window, Window.DecorView);
        if (insetsController is not null)
        {
            insetsController.Hide(WindowInsetsCompat.Type.SystemBars());
            insetsController.SystemBarsBehavior =
                WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
        }
    }
}
