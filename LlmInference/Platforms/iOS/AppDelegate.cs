using System.Reflection;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace LlmInference;

[Register ("AppDelegate")]
public class AppDelegate : UIResponder, IUIApplicationDelegate
{
    [Export ("window")]
    public UIWindow Window { get; set; }

    private const string HasLaunchedBeforeKey = "com.google.mediapipe.InferenceExampleApp.hasLaunchedBefore";

    public bool FinishedLaunching(
        UIApplication application,
        NSDictionary launchOptions)
    {
        // First launch cleanup
        CleanupFirstLaunchIfNeeded();

        // Fallback for non-Scene-based launches
        if (OperatingSystem.IsIOSVersionAtLeast(13, 0))
        {
            // Handled by SceneDelegate
        }
        else
        {
            var window = new UIWindow(frame: UIScreen.MainScreen.Bounds);
            window.RootViewController = AppDelegate.MakeRootViewController();
            Window = window;
            window.MakeKeyAndVisible();
        }

        return true;
    }

    // MARK: UISceneSession Lifecycle
    public UISceneConfiguration GetConfiguration(
        UIApplication application,
        UISceneSession connectingSceneSession,
        UISceneConnectionOptions options)
    {
        var sceneConfig = new UISceneConfiguration(null, sessionRole: connectingSceneSession.Role);
        sceneConfig.DelegateClass = new Class(typeof(SceneDelegate));
        return sceneConfig;
    }

    public static UIViewController MakeRootViewController()
    {
        var navigationController = new UINavigationController(rootViewController: new ModelSelectionViewController());

        // Customize navigation bar appearance
        var appearance = new UINavigationBarAppearance();
        appearance.ConfigureWithOpaqueBackground();
        appearance.BackgroundColor = Metadata.GlobalColor;
        appearance.TitleTextAttributes = new UIStringAttributes
            { ForegroundColor = UIColor.White };
        navigationController.NavigationBar.StandardAppearance = appearance;
        navigationController.NavigationBar.ScrollEdgeAppearance = appearance;
        navigationController.NavigationBar.CompactAppearance = appearance;
        navigationController.NavigationBar.TintColor = UIColor.White;

        return navigationController;
    }

    private static void CleanupFirstLaunchIfNeeded()
    {
        var defaults = NSUserDefaults.StandardUserDefaults;
        if (defaults.BoolForKey(HasLaunchedBeforeKey))
            return;

        var keys = typeof(Model)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(Model))
            .Select(field => ((Model)field.GetValue(null)).LicenseAcknowledgedKey)
            .Prepend(AuthConfig.AccessTokenKeychainKey)
            .Prepend(AuthConfig.CodeVerifierKeychainKey)
            .ToArray();
        KeychainHelper.Clear(keys);

        defaults.SetBool(true, HasLaunchedBeforeKey);
        defaults.Synchronize();
    }
}

// MARK: - SceneDelegate
[Register ("SceneDelegate")]
public class SceneDelegate : UIResponder, IUIWindowSceneDelegate
{
    [Export ("window")]
    public UIWindow Window { get; set; }

    public void WillConnect(
        UIScene scene,
        UISceneSession session,
        UISceneConnectionOptions connectionOptions)
    {
        if (scene is not UIWindowScene windowScene)
            return;

        var window = new UIWindow(windowScene: windowScene);
        window.RootViewController = AppDelegate.MakeRootViewController();
        this.Window = window;
        Window.MakeKeyAndVisible();
    }
}
