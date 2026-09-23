using UIKit;

namespace LlmInference;

public class HuggingFaceFlowViewController : UIViewController
{
    private readonly HuggingFaceFlowViewModel viewModel;
    private readonly DownloadViewModel downloadViewModel;
    private UIViewController currentChildViewController;

    public HuggingFaceFlowViewController(HuggingFaceFlowViewModel viewModel)
    {
        this.viewModel = viewModel;
        downloadViewModel = new DownloadViewModel(viewModel.ModelCategory);
    }

    public HuggingFaceFlowViewController(IntPtr handle) : base(handle) { }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        View.BackgroundColor = UIColor.SystemBackground;

        BindViewModel();
    }

    private void BindViewModel()
    {
        viewModel.OnActionChanged = action => UpdateCurrentView(action);
        downloadViewModel.OnStateChanged = state => UpdateViewBasedOnDownloadState(state);

        // Set initial view
        UpdateCurrentView(viewModel.Action);
    }

    private void UpdateCurrentView(HuggingFaceFlowViewModel.ActionEnum action)
    {
        // Remove previous child
        if (currentChildViewController != null)
        {
            currentChildViewController.WillMoveToParentViewController(parent: null);
            currentChildViewController.View.RemoveFromSuperview();
            currentChildViewController.RemoveFromParentViewController();
        }

        UIViewController newViewController;
        if (action == HuggingFaceFlowViewModel.ActionEnum.AcknowledgeLicense)
        {
            var acknowledgeViewModel = new AcknowledgeLicenseViewModel(
                url: viewModel.ModelCategory.LicenseURL,
                licenseAcknowledgedKey: viewModel.ModelCategory.LicenseAcknowledgedKey);
            newViewController = new AcknowledgeLicenseViewController(viewModel: acknowledgeViewModel,
                viewModel.UpdateState);
        }
        else
        {
            newViewController = new DownloadViewController(downloadViewModel, () =>
            {
                viewModel.UpdateState();
                DismissViewController(animated: true, completionHandler:null);
            });
        }

        AddChildViewController(newViewController);
        newViewController.View.Frame = View.Bounds;
        newViewController.View.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
        View.AddSubview(newViewController.View);
        newViewController.DidMoveToParentViewController(this);
        currentChildViewController = newViewController;
    }

    private void UpdateViewBasedOnDownloadState(DownloadViewModel.DownloadState state)
    {
        if (state is DownloadViewModel.DownloadState.Error errorState)
        {
            ShowErrorAlert(errorState.error);
        }
    }

    private void ShowErrorAlert(DownloadViewModel.DownloadError error)
    {
        var alert = UIAlertController.Create(title: error.ErrorDescription, message: error.FailureReason, preferredStyle: UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create(title: "OK", style: UIAlertActionStyle.Default, handler: _ =>
        {
            if (error.NetworkError?.Error == NetworkService.NetworkError.Forbidden)
                viewModel.UpdateState();
            else
                downloadViewModel.State = new DownloadViewModel.DownloadState.NotInitiated();
        }));
        PresentViewController(alert, animated: true, null);
    }
}
