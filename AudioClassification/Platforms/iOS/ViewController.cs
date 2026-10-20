using Foundation;
using MediaPipeTasksAudio;
using ObjCRuntime;
using UIKit;

namespace AudioClassification;

public class ViewController : UIViewController, IUITableViewDataSource, IUITableViewDelegate, IAudioClassifierHelperDelegate
{
    private UILabel titleLabel = new();
    private UILabel statusLabel = new();
    private UIButton toggleRecordButton = new(type: UIButtonType.System);
    private UILabel thresholdLabel = new();
    private UISlider thresholdSlider = new();
    private UILabel maxResultsLabel = new();
    private UIStepper maxResultsStepper = new();
    private UILabel inferenceTimeLabel = new();
    private UITableView resultsTableView = new();

    private AudioClassifierHelper helper;
    private List<MPPCategory> categories = new();
    private bool isRecording = false;

    override public void ViewDidLoad()
    {
        base.ViewDidLoad();
        View.BackgroundColor = UIColor.SystemBackground;
        SetupUI();
        InitializeHelper();
    }

    override public void ViewWillDisappear(bool animated)
    {
        base.ViewWillDisappear(animated);
        if (isRecording)
        {
            ToggleRecording();
        }
    }

    private void SetupUI()
    {
        titleLabel.Text = "Audio Classifier";
        titleLabel.Font = UIFont.BoldSystemFontOfSize(size: 24);
        titleLabel.TextAlignment = UITextAlignment.Center;
        titleLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(titleLabel);

        statusLabel.Text = "Tap Start to classify ambient sound";
        statusLabel.Font = UIFont.SystemFontOfSize(size: 14);
        statusLabel.TextColor = UIColor.SecondaryLabel;
        statusLabel.TextAlignment = UITextAlignment.Center;
        statusLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(statusLabel);

        toggleRecordButton.SetTitle("Start Recording", forState: UIControlState.Normal);
        toggleRecordButton.TitleLabel?.Font = UIFont.SystemFontOfSize(size: 17, weight: UIFontWeight.Semibold);
        toggleRecordButton.BackgroundColor = UIColor.SystemRed;
        toggleRecordButton.SetTitleColor(UIColor.White, forState: UIControlState.Normal);
        toggleRecordButton.Layer.CornerRadius = 10;
        toggleRecordButton.AddTarget(this, sel: new Selector(nameof(ToggleRecording)), events: UIControlEvent.TouchUpInside);
        toggleRecordButton.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(toggleRecordButton);

        var settingsCard = new UIStackView();
        settingsCard.Axis = UILayoutConstraintAxis.Vertical;
        settingsCard.Spacing = 10;
        settingsCard.Layer.BorderColor = UIColor.Separator.CGColor;
        settingsCard.Layer.BorderWidth = 1;
        settingsCard.Layer.CornerRadius = 10;
        settingsCard.LayoutMarginsRelativeArrangement = true;
        settingsCard.LayoutMargins = new UIEdgeInsets(top: 10, left: 14, bottom: 10, right: 14);
        settingsCard.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(settingsCard);

        var thresholdStack = new UIStackView();
        thresholdStack.Axis = UILayoutConstraintAxis.Horizontal;
        thresholdStack.Distribution = UIStackViewDistribution.EqualSpacing;
        thresholdLabel.Text = "Threshold: 0.30";
        thresholdLabel.Font = UIFont.SystemFontOfSize(size: 14, weight: UIFontWeight.Medium);
        thresholdStack.AddArrangedSubview(thresholdLabel);

        thresholdSlider.MinValue = 0.0f;
        thresholdSlider.MaxValue = 0.95f;
        thresholdSlider.Value = 0.30f;
        thresholdSlider.AddTarget(this, sel: new Selector(nameof(ThresholdChanged)), events: UIControlEvent.ValueChanged);
        thresholdSlider.WidthAnchor.ConstraintEqualTo(constant: 160).Active = true;
        thresholdStack.AddArrangedSubview(thresholdSlider);
        settingsCard.AddArrangedSubview(thresholdStack);

        var maxResultsStack = new UIStackView();
        maxResultsStack.Axis = UILayoutConstraintAxis.Horizontal;
        maxResultsStack.Distribution = UIStackViewDistribution.EqualSpacing;
        maxResultsLabel.Text = "Max Results: 3";
        maxResultsLabel.Font = UIFont.SystemFontOfSize(size: 14, weight: UIFontWeight.Medium);
        maxResultsStack.AddArrangedSubview(maxResultsLabel);

        maxResultsStepper.MinimumValue = 1;
        maxResultsStepper.MaximumValue = 10;
        maxResultsStepper.Value = 3;
        maxResultsStepper.AddTarget(this, sel: new Selector(nameof(MaxResultsChanged)), events: UIControlEvent.ValueChanged);
        maxResultsStack.AddArrangedSubview(maxResultsStepper);
        settingsCard.AddArrangedSubview(maxResultsStack);

        inferenceTimeLabel.Font = UIFont.MonospacedDigitSystemFontOfSize(size: 13, weight: (int)UIFontWeight.Regular);
        inferenceTimeLabel.TextColor = UIColor.SecondaryLabel;
        inferenceTimeLabel.Text = "Inference Time: -- ms";
        inferenceTimeLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(inferenceTimeLabel);

        var resultsHeaderLabel = new UILabel();
        resultsHeaderLabel.Text = "Detected Categories:";
        resultsHeaderLabel.Font = UIFont.SystemFontOfSize(size: 15, weight: UIFontWeight.Semibold);
        resultsHeaderLabel.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(resultsHeaderLabel);

        resultsTableView.DataSource = this;
        resultsTableView.Delegate = this;
        resultsTableView.RegisterClassForCellReuse(typeof(UITableViewCell), "AudioResultCell");
        resultsTableView.Layer.BorderColor = UIColor.Separator.CGColor;
        resultsTableView.Layer.BorderWidth = 1;
        resultsTableView.Layer.CornerRadius = 8;
        resultsTableView.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(resultsTableView);

        NSLayoutConstraint.ActivateConstraints(new NSLayoutConstraint[]
        {
            titleLabel.TopAnchor.ConstraintEqualTo(anchor: View.SafeAreaLayoutGuide.TopAnchor, constant: 16),
            titleLabel.LeadingAnchor.ConstraintEqualTo(anchor: View.LeadingAnchor, constant: 20),
            titleLabel.TrailingAnchor.ConstraintEqualTo(anchor: View.TrailingAnchor, constant: -20),

            statusLabel.TopAnchor.ConstraintEqualTo(anchor: titleLabel.BottomAnchor, constant: 6),
            statusLabel.LeadingAnchor.ConstraintEqualTo(anchor: View.LeadingAnchor, constant: 20),
            statusLabel.TrailingAnchor.ConstraintEqualTo(anchor: View.TrailingAnchor, constant: -20),

            toggleRecordButton.TopAnchor.ConstraintEqualTo(anchor: statusLabel.BottomAnchor, constant: 16),
            toggleRecordButton.LeadingAnchor.ConstraintEqualTo(anchor: View.LeadingAnchor, constant: 20),
            toggleRecordButton.TrailingAnchor.ConstraintEqualTo(anchor: View.TrailingAnchor, constant: -20),
            toggleRecordButton.HeightAnchor.ConstraintEqualTo(constant: 46),

            settingsCard.TopAnchor.ConstraintEqualTo(anchor: toggleRecordButton.BottomAnchor, constant: 14),
            settingsCard.LeadingAnchor.ConstraintEqualTo(anchor: View.LeadingAnchor, constant: 20),
            settingsCard.TrailingAnchor.ConstraintEqualTo(anchor: View.TrailingAnchor, constant: -20),

            inferenceTimeLabel.TopAnchor.ConstraintEqualTo(anchor: settingsCard.BottomAnchor, constant: 12),
            inferenceTimeLabel.LeadingAnchor.ConstraintEqualTo(anchor: View.LeadingAnchor, constant: 20),
            inferenceTimeLabel.TrailingAnchor.ConstraintEqualTo(anchor: View.TrailingAnchor, constant: -20),

            resultsHeaderLabel.TopAnchor.ConstraintEqualTo(anchor: inferenceTimeLabel.BottomAnchor, constant: 10),
            resultsHeaderLabel.LeadingAnchor.ConstraintEqualTo(anchor: View.LeadingAnchor, constant: 20),
            resultsHeaderLabel.TrailingAnchor.ConstraintEqualTo(anchor: View.TrailingAnchor, constant: -20),

            resultsTableView.TopAnchor.ConstraintEqualTo(anchor: resultsHeaderLabel.BottomAnchor, constant: 6),
            resultsTableView.LeadingAnchor.ConstraintEqualTo(anchor: View.LeadingAnchor, constant: 20),
            resultsTableView.TrailingAnchor.ConstraintEqualTo(anchor: View.TrailingAnchor, constant: -20),
            resultsTableView.BottomAnchor.ConstraintEqualTo(anchor: View.SafeAreaLayoutGuide.BottomAnchor, constant: -16)
        });
    }

    private void InitializeHelper()
    {
        var modelPath = NSBundle.MainBundle.PathForResource("yamnet", "tflite");
        if (modelPath == null)
        {
            statusLabel.Text = "Error: yamnet.tflite not found in bundle";
            return;
        }
        helper = new AudioClassifierHelper(
            modelPath: modelPath,
            scoreThreshold: thresholdSlider.Value,
            maxResults: (int)maxResultsStepper.Value
        );
        helper?.Delegate = this;
    }

    [Export(nameof(ToggleRecording))]
    private void ToggleRecording()
    {
        if (isRecording)
        {
            isRecording = false;
            helper?.StopLiveClassification();
            toggleRecordButton.SetTitle("Start Recording", forState: UIControlState.Normal);
            toggleRecordButton.BackgroundColor = UIColor.SystemRed;
            statusLabel.Text = "Stopped recording";
        }
        else
        {
            isRecording = true;
            helper?.ScoreThreshold = thresholdSlider.Value;
            helper?.MaxResults = (int)maxResultsStepper.Value;
            helper?.StartLiveClassification();
            toggleRecordButton.SetTitle("Stop Recording", forState: UIControlState.Normal);
            toggleRecordButton.BackgroundColor = UIColor.SystemGray;
            statusLabel.Text = "Listening to ambient audio...";
        }
    }

    [Export(nameof(ThresholdChanged))]
    private void ThresholdChanged()
    {
        var value = thresholdSlider.Value;
        thresholdLabel.Text = String.Format(format: "Threshold: {0:F2}", value);
        helper?.ScoreThreshold = value;
    }

    [Export(nameof(MaxResultsChanged))]
    private void MaxResultsChanged()
    {
        var value = (int)maxResultsStepper.Value;
        maxResultsLabel.Text = String.Format(format: "Max Results: {0}", value);
        helper?.MaxResults = value;
    }

    // MARK: - AudioClassifierHelperDelegate

    public void DidFinishClassification(
        AudioClassifierHelper helper,
        List<MPPCategory> categories,
        double inferenceTime)
    {
        this.categories = categories;
        this.inferenceTimeLabel.Text = String.Format(format: "Inference Time: {0:F1} ms", inferenceTime);
        this.resultsTableView.ReloadData();
    }

    public void DidFailWithError(
        AudioClassifierHelper helper,
        NSError error)
    {
        this.statusLabel.Text = "Error: " + error.LocalizedDescription;
        if (isRecording)
        {
            ToggleRecording();
        }
    }

    // MARK: - UITableViewDataSource

    public nint RowsInSection(UITableView tableView, nint section) =>
        categories.Count;

    public UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
    {
        var cell = new UITableViewCell(style: UITableViewCellStyle.Value1, reuseIdentifier: "AudioResultCell");
        var category = categories[indexPath.Row];
        var name = category.CategoryName ?? "Unknown";
        cell.TextLabel?.Text = name;
        cell.TextLabel?.Font = UIFont.SystemFontOfSize(size: 15, weight: (int)UIFontWeight.Medium);
        cell.DetailTextLabel?.Text = String.Format(format: "{0:F1}%", category.Score * 100.0);
        cell.DetailTextLabel?.Font = UIFont.MonospacedDigitSystemFontOfSize(size: 15, weight: (int)UIFontWeight.Bold);
        return cell;
    }
}
