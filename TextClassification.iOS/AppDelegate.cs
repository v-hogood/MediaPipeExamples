namespace TextClassification;

[Register ("AppDelegate")]
public class AppDelegate : UIResponder, IUIApplicationDelegate
{
        [Export ("window")]
        public UIWindow Window { get; set; }
}
