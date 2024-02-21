using ObjCRuntime;

namespace ImageClassification;

public interface InferenceResultDeliveryDelegate
{
    void DidPerformInference(ResultBundle? result);
    void DidPerformInference(ResultBundle? result, int index);
}

public interface InterfaceUpdatesDelegate
{
    void ShouldClicksBeEnabled(bool isEnabled);
}

//
// The view controller is responsible for presenting and handling the tabbed controls for switching between the live camera feed and
// media library view controllers. It also handles the presentation of the inferenceVC
//
public partial class RootViewController : UIViewController
{
    public RootViewController(NativeHandle handle) : base(handle) { }

    public struct Constants
    {
        public const float InferenceBottomHeight = 240.0f;
        public const float ExpandButtonHeight = 41.0f;
        public const float ExpandButtonTopSpace = 10.0f;
        public const string MediaLibraryViewControllerStoryBoardId = "MEDIA_LIBRARY_VIEW_CONTROLLER";
        public const string CameraViewControllerStoryBoardId = "CAMERA_VIEW_CONTROLLER";
        public const string StoryBoardName = "Main";
        public const string InferenceVCEmbedSegueName = "EMBED";
        public const int TabBarItemsCount = 2;
    }

    public BottomSheetViewController bottomSheetViewController;
    public CameraViewController cameraViewController;
    public MediaLibraryViewController mediaLibraryViewController;

    private bool isObserving = false;
    private float TotalBottomSheetHeight
    {
        get
        {
            if (bottomSheetViewController == null)
                return 0;

            return bottomSheetViewController.toggleBottomSheetButton.Selected ?
                Constants.InferenceBottomHeight - (float)this.View.SafeAreaInsets.Bottom + bottomSheetViewController.CollapsedHeight :
                Constants.ExpandButtonHeight + Constants.ExpandButtonTopSpace + bottomSheetViewController.CollapsedHeight;
        }
    }

    override public void ViewDidLoad()
    {
        base.ViewDidLoad();

        bottomSheetViewController.UIEnabled = true;
        runningModeTabbar.SelectedItem = runningModeTabbar.Items?.First();
        runningModeTabbar.Delegate = this;
        InstantiateCameraViewController();
        SwitchTo(childViewController: cameraViewController, fromViewController: null);
    }

    override public void ViewWillLayoutSubviews()
    {
        base.ViewWillLayoutSubviews();
        if (bottomSheetViewController == null)
            return;
        bottomViewHeightConstraint.Constant = bottomSheetViewController.CollapsedHeight + Constants.InferenceBottomHeight;
        if (bottomSheetViewController.toggleBottomSheetButton.Selected == false)
        {
            bottomSheetViewBottomSpace.Constant = -Constants.InferenceBottomHeight +
                +Constants.ExpandButtonHeight
                + this.View.SafeAreaInsets.Bottom
                + Constants.ExpandButtonTopSpace;
        }
        else
        {
            bottomSheetViewBottomSpace.Constant = 0;
        }
    }

    override public UIStatusBarStyle PreferredStatusBarStyle()
    {
        return UIStatusBarStyle.LightContent;
    }

    override public void PrepareForSegue(UIStoryboardSegue segue, NSObject sender)
    {
        base.PrepareForSegue(segue: segue, sender: sender);
        if (segue.Identifier.Equals(Constants.InferenceVCEmbedSegueName))
        {
            bottomSheetViewController = segue.DestinationViewController as BottomSheetViewController;
            bottomSheetViewController.Delegate = this;
            bottomViewHeightConstraint.Constant = Constants.InferenceBottomHeight;
            View.LayoutSubviews();
        }
    }

    private void InstantiateCameraViewController()
    {
        if (cameraViewController != null)
            return;

        var viewController = UIStoryboard.FromName(
            name: Constants.StoryBoardName, storyboardBundleOrNil: NSBundle.MainBundle)
            .InstantiateViewController(
                identifier: Constants.CameraViewControllerStoryBoardId) as CameraViewController;
        if (viewController == null)
            return;

        viewController.InferenceResultDeliveryDelegate = this;
        viewController.InterfaceUpdatesDelegate = this;

        cameraViewController = viewController;
    }

    private void StartObserveMaxResultsChanges()
    {
        NSNotificationCenter.DefaultCenter
          .AddObserver(this,
                       aSelector: new ObjCRuntime.Selector("changebottomViewHeightConstraint"),
                       aName: (NSString)InferenceConfigManager.MaxResultChangeNotificationName,
                       anObject: null);
        isObserving = true;
    }

    private void StopObserveMaxResultsChanges()
    {
        if (isObserving)
        {
            NSNotificationCenter.DefaultCenter
              .RemoveObserver(this,
                              aName: InferenceConfigManager.MaxResultChangeNotificationName,
                              anObject: null);
        }
        isObserving = false;
    }

    [Export("changebottomViewHeightConstraint")]
    private void ChangebottomViewHeightConstraint()
    {
        if (bottomSheetViewController == null)
            return;
        bottomViewHeightConstraint.Constant = bottomSheetViewController.CollapsedHeight + Constants.InferenceBottomHeight;
    }

    public void InstantiateMediaLibraryViewController()
    {
        if (mediaLibraryViewController != null)
            return;

        var viewController = UIStoryboard.FromName(name: Constants.StoryBoardName, storyboardBundleOrNil: NSBundle.MainBundle)
            .InstantiateViewController(
                identifier: Constants.MediaLibraryViewControllerStoryBoardId)
            as MediaLibraryViewController;
        if (viewController == null)
            return;

        viewController.InterfaceUpdatesDelegate = this;
        viewController.InferenceResultDeliveryDelegate = this;
        mediaLibraryViewController = viewController;
    }

    public void UpdateMediaLibraryControllerUI()
    {
        if (mediaLibraryViewController == null)
            return;

        mediaLibraryViewController.LayoutUIElements(
          height: this.TotalBottomSheetHeight);
    }
}

partial class RootViewController : IUITabBarDelegate
{
    public void SwitchTo(
        UIViewController childViewController,
        UIViewController fromViewController)
    {
        fromViewController?.WillMoveToParentViewController(parent: null);
        fromViewController?.View.RemoveFromSuperview();
        fromViewController?.RemoveFromParentViewController();

        if (childViewController == null)
            return;

        AddChildViewController(childViewController);
        childViewController.View.TranslatesAutoresizingMaskIntoConstraints = false;
        tabBarContainerView.AddSubview(childViewController.View);
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            childViewController.View.LeadingAnchor.ConstraintEqualTo(
                anchor: tabBarContainerView.LeadingAnchor,
                constant: 0),
            childViewController.View.TrailingAnchor.ConstraintEqualTo(
                anchor: tabBarContainerView.TrailingAnchor,
                constant: 0),
            childViewController.View.TopAnchor.ConstraintEqualTo(
                anchor: tabBarContainerView.TopAnchor,
                constant: 0),
            childViewController.View.BottomAnchor.ConstraintEqualTo(
                anchor: tabBarContainerView.BottomAnchor,
                constant: 0)
        });
        childViewController.DidMoveToParentViewController(parent: this);
    }

    [Export("tabBar:didSelectItem:")]
    void ItemSelected(UITabBar tabBar, UITabBarItem item)
    {
        var tabBarItems = tabBar.Items;
        if (tabBarItems == null || tabBarItems.Count() != RootViewController.Constants.TabBarItemsCount)
            return;

        UIViewController fromViewController = null;
        UIViewController toViewController = null;

        if (item == tabBarItems[0])
        {
            fromViewController = mediaLibraryViewController;
            toViewController = cameraViewController;
        }
        else if (item == tabBarItems[1])
        {
            InstantiateMediaLibraryViewController();
            fromViewController = cameraViewController;
            toViewController = mediaLibraryViewController;
        }

        SwitchTo(
            childViewController: toViewController,
            fromViewController: fromViewController);
        this.ShouldClicksBeEnabled(true);
        this.UpdateMediaLibraryControllerUI();
    }
}

partial class RootViewController : InferenceResultDeliveryDelegate
{
    public void DidPerformInference(ResultBundle? result)
    {
        var inferenceTimeString = "";

        var inferenceTime = result?.inferenceTime;
        if (inferenceTime != null)
        {
            inferenceTimeString = string.Format("{0:0.00}ms", inferenceTime);
        }
        bottomSheetViewController?.Update(inferenceTimeString: inferenceTimeString,
                                          result: result?.imageClassifierResults.First());
    }

    public void DidPerformInference(ResultBundle? result, int index)
    {
        var inferenceTimeString = "";

        var inferenceTime = result?.inferenceTime;
        if (inferenceTime != null)
        {
            inferenceTimeString = string.Format("{0:0.00}ms", inferenceTime);
        }
        var imageClassifierResults = result?.imageClassifierResults;
        if (imageClassifierResults != null && index < imageClassifierResults.Count())
        {
            bottomSheetViewController?.Update(inferenceTimeString: inferenceTimeString,
                                              result: imageClassifierResults[index]);
        }
    }
}

partial class RootViewController : InterfaceUpdatesDelegate
{
    public void ShouldClicksBeEnabled(bool isEnabled)
    {
        bottomSheetViewController.UIEnabled = isEnabled;
    }
}

partial class RootViewController : BottomSheetViewControllerDelegate
{
    public void ViewController(
        BottomSheetViewController viewController,
        bool isOpen)
    {
        if (isOpen == true)
        {
            bottomSheetViewBottomSpace.Constant = 0;
        }
        else
        {
            bottomSheetViewBottomSpace.Constant = -Constants.InferenceBottomHeight
                + Constants.ExpandButtonHeight
                + this.View.SafeAreaInsets.Bottom
                + Constants.ExpandButtonTopSpace;
        }

        UIView.Animate(duration: 0.3, () =>
        {
            this.View.LayoutSubviews();
            this.UpdateMediaLibraryControllerUI();
        });
    }
}
