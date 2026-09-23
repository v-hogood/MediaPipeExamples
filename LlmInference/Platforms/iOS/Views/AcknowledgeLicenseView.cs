using SafariServices;
using UIKit;

namespace LlmInference;

public class AcknowledgeLicenseViewController : UIViewController
{
    private const string instructionText = @"When you click on acknowledge license, you will be redirected to the model page on Hugging Face. 
        Please scroll down, log in to Hugging Face, and then click on ""Acknowledge License"".";
    private const string acknowledgeLicenseButtonTitle = "Acknowledge License";
    private const string continueButtonTitle = "Continue";

    private readonly AcknowledgeLicenseViewModel viewModel;
    private readonly Action onLicenseViewed;

    private readonly UILabel instructionLabel = new()
    {
        Text = instructionText,
        Font = UIFont.PreferredCallout,
        TextColor = UIColor.SecondaryLabel,
        Lines = 0,
        TextAlignment = UITextAlignment.Center
    };

    private readonly HuggingFaceButton acknowledgeButton = new(acknowledgeLicenseButtonTitle);
    private readonly RoundedRectButton continueButton = new(continueButtonTitle);

    public AcknowledgeLicenseViewController(AcknowledgeLicenseViewModel viewModel, Action onLicenseViewed)
    {
        this.viewModel = viewModel;
        this.onLicenseViewed = onLicenseViewed;
    }

    public AcknowledgeLicenseViewController(IntPtr handle) : base(handle) { }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        View.BackgroundColor = UIColor.SystemBackground;
        SetupLayout();
        SetupActions();
        BindViewModel();
    }

    private void SetupLayout()
    {
        var stackView = new UIStackView(views:new UIView[] { instructionLabel, acknowledgeButton, continueButton })
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 20,
            Alignment = UIStackViewAlignment.Fill,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        View.AddSubview(stackView);
        stackView.TranslatesAutoresizingMaskIntoConstraints = false;

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            stackView.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor, constant: 20),
            stackView.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor, constant: -20),
            stackView.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor),
      
            acknowledgeButton.HeightAnchor.ConstraintEqualTo(constant: 50),
            continueButton.HeightAnchor.ConstraintEqualTo(constant: 50)
        });
    }

    private void SetupActions()
    {
        acknowledgeButton.TouchUpInside += (_, _) => DidTapAcknowledge();
        continueButton.TouchUpInside += (_, _) => DidTapContinue();
    }

    private void BindViewModel()
    {
        viewModel.OnDisableContinueChanged = disabled =>
        {
            continueButton.Enabled = !disabled;
            continueButton.Alpha = disabled ? 0.5f : 1f;
        };
        // Set initial state
        viewModel.OnDisableContinueChanged?.Invoke(viewModel.DisableContinue);
    }

    private void DidTapAcknowledge()
    {
        var safariVC = new SFSafariViewController(url: viewModel.Url);
        PresentViewController(safariVC, true, () =>
            viewModel.HandleLicenseViewed());
    }

    private void DidTapContinue() => onLicenseViewed?.Invoke();
}
