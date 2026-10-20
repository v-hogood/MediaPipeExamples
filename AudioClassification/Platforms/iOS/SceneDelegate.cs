using Foundation;
using UIKit;

namespace AudioClassification;

[Register("SceneDelegate")]
public class SceneDelegate: UIResponder, IUIWindowSceneDelegate
{
    [Export("window")]
    public UIWindow Window { get; set; }

    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(
        UIScene scene,
        UISceneSession session,
        UISceneConnectionOptions connectionOptions)
    {
        var windowScene = scene as UIWindowScene;
        if (windowScene == null) return;
        Window = new UIWindow(windowScene: windowScene);
        Window?.RootViewController = new ViewController();
        Window?.MakeKeyAndVisible();
    }
}
