using AuthenticationServices;
using Foundation;
using UIKit;

namespace LlmInference;

public class DownloadViewController : UIViewController, IASWebAuthenticationPresentationContextProviding
{
    private readonly DownloadViewModel viewModel;
    private readonly Action onDownloadCompletion;

    // UI Components
    private readonly HuggingFaceButton downloadButton = new(title: "");

    private readonly UIStackView progressContainer = new();
    private readonly UIProgressView progressView = new(UIProgressViewStyle.Default)
    {
        ProgressTintColor = Metadata.GlobalColor
    };

    private readonly UILabel progressLabel = new()
    {
        TextAlignment = UITextAlignment.Center,
        Font = UIFont.PreferredSubheadline
    };

    private readonly RoundedRectButton cancelButton =
        new(title: "Cancel", backgroundColor: UIColor.SystemGray5, foregroundColor: UIColor.Label);

    public DownloadViewController(DownloadViewModel viewModel, Action onDownloadCompletion)
    {
        this.viewModel = viewModel;
        this.onDownloadCompletion = onDownloadCompletion;
    }

    public DownloadViewController(IntPtr handle) : base(handle) { }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        View.BackgroundColor = UIColor.SystemBackground;
        SetupLayout();
        BindViewModel();

        downloadButton.TouchUpInside += (_, _) => DidTapDownload();
        cancelButton.TouchUpInside += (_, _) => DidTapCancel();
    }

    public override void ViewWillDisappear(bool animated)
    {
        base.ViewWillDisappear(animated);
        if (IsMovingFromParentViewController || IsBeingDismissed)
            viewModel.CancelDownload();
    }

    void SetupLayout()
    {
        var loadingLabel = new UILabel
        {
            Text = "Downloading...",
            TextAlignment = UITextAlignment.Center,
            Font = UIFont.PreferredHeadline
        };
        progressContainer.Axis = UILayoutConstraintAxis.Vertical;
        progressContainer.Spacing = 15;
        progressContainer.Alignment = UIStackViewAlignment.Fill;
        progressContainer.AddArrangedSubview(loadingLabel);
        progressContainer.AddArrangedSubview(progressView);
        progressContainer.AddArrangedSubview(progressLabel);
        progressContainer.AddArrangedSubview(cancelButton);

        downloadButton.TranslatesAutoresizingMaskIntoConstraints = false;
        progressContainer.TranslatesAutoresizingMaskIntoConstraints = false;

        View.AddSubview(downloadButton);
        View.AddSubview(progressContainer);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            downloadButton.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
            downloadButton.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor),
            downloadButton.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor, constant: 40),
            downloadButton.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor, constant: -40),
            downloadButton.HeightAnchor.ConstraintEqualTo(50),

            progressContainer.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor, constant: 40),
            progressContainer.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor, constant: -40),
            progressContainer.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor),
            cancelButton.HeightAnchor.ConstraintEqualTo(constant: 50)
        });
    }

    public void BindViewModel()
    {
        // Update button title based on auth requirements
        var title = $"Download {viewModel.ModelName}";
        if (viewModel.AuthRequired)
            title = "Sign in and " + title;
        downloadButton.SetTitle(title, forState: UIControlState.Normal);

        // Bind state changes
        viewModel.OnStateChanged = state => UpdateUI(state);

        // Bind progress
        viewModel.OnProgressChanged = progress =>
        {
            progressView.Progress = (float)(progress / 100.0);
            progressLabel.Text = $"Current progress: {(int)progress}%";
        };

        // Configure initial states
        UpdateUI(viewModel.State);
        progressView.Progress = (float)(viewModel.Progress / 100.0);
        progressLabel.Text = $"Current progress: {(int)viewModel.Progress}%";
    }

    private void UpdateUI(DownloadViewModel.DownloadState state)
    {
        var showDownload = state is DownloadViewModel.DownloadState.NotInitiated || state is DownloadViewModel.DownloadState.LoginRequired || state is DownloadViewModel.DownloadState.Error;
        downloadButton.Hidden = !showDownload;
        progressContainer.Hidden = showDownload;
        if (state is DownloadViewModel.DownloadState.Completed)
            onDownloadCompletion?.Invoke();
    }

    private void DidTapDownload()
    {
        if (viewModel.AuthRequired)
            _ = PerformAuthentication();
        else
            viewModel.Download();
    }

    private void DidTapCancel()
    {
        viewModel.CancelDownload();
    }

    private async Task PerformAuthentication()
    {
        var url = viewModel.GetAuthorizationUrl();
        if (url == null) return;
        var session = new ASWebAuthenticationSession(
            url: url,
            callbackUrlScheme: "com.google.mediapipe.examples.llminference",
            (callbackUrl, error) =>
        {
            if (error != null)
            {
                viewModel.HandleWebAuthenticationError(error);
            }
            else if (callbackUrl != null)
            {
                _ = CompleteAuthentication(callbackUrl);
            }
        });
        session.PresentationContextProvider = this;
        session.PrefersEphemeralWebBrowserSession = true;
        session.Start();

        await Task.CompletedTask;
    }

    private async Task CompleteAuthentication(NSUrl callbackUrl)
    {
        if (await viewModel.HandleAuthenticationCallback(callbackUrl))
            viewModel.Download();
    }

    // MARK: - ASWebAuthenticationPresentationContextProviding
    public UIWindow GetPresentationAnchor(ASWebAuthenticationSession session) => View.Window ?? new UIWindow();
}
