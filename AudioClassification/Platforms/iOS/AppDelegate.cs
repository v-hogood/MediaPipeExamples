using Foundation;
using UIKit;

namespace AudioClassification;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    override public bool FinishedLaunching(
        UIApplication application,
        NSDictionary launchOptions)
    {
        return true;
    }

    // MARK: UISceneSession Lifecycle

    override public UISceneConfiguration GetConfiguration(
        UIApplication application,
        UISceneSession connectingSceneSession,
        UISceneConnectionOptions options)
    {
        return new UISceneConfiguration("Default Configuration", connectingSceneSession.Role);
    }
}
