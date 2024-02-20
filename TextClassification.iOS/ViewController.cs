using CoreFoundation;
using MediaPipeTasksText;
using ObjCRuntime;
using static TextClassification.TextClassifierHelper;

namespace TextClassification;

public partial class ViewController : UIViewController
{
    public ViewController(NativeHandle handle) : base(handle) { }

    TextClassifierHelper textClassifier;
    MPPCategory[] categories;

    DispatchQueue backgroundQueue = new("com.google.mediapipe.textclassification",
        new DispatchQueue.Attributes() { QualityOfService = DispatchQualityOfService.UserInteractive });

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        SetupUI();
        SetupTableView();
        HideKeyboardWhenTappedAround();
        inputTextView.Delegate = this;

        // Setup the text classifier object.
        backgroundQueue.DispatchAsync(() =>
            textClassifier = new TextClassifierHelper(model: Constants.DefaultModel));
    }

    [Export("classifyButtonTouchUpInside:")]
    public void ClassifyButtonTouchUpInside(UIButton sender)
    {
        var inputText = inputTextView.Text;
        var timeStart = new NSDate();
        classifyButton.Enabled = false;
        inputTextView.UserInteractionEnabled = false;
        clearButton.Enabled = false;

        backgroundQueue.DispatchAsync(() =>
        {
            var result = textClassifier?.Classify(text: inputText);
            var categories = result?.ClassificationResult.Classifications[0].Categories;

            // Show result on UI
            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                var inferenceTime = new NSDate().SecondsSinceReferenceDate - timeStart.SecondsSinceReferenceDate;
                this.categories = categories;
                this.inferenceTimeLabel.Text = string.Format("{0:0.00}ms", inferenceTime * 1000);
                this.tableView.ReloadData();

                // Re-enable input text UI elements
                this.classifyButton.Enabled = true;
                this.inputTextView.UserInteractionEnabled = true;
                this.clearButton.Enabled = true;
            });
        });
    }

    [Export("clearButtonTouchUpInside:")]
    public void ClearButtonTouchUpInside(UIButton sender)
    {
        inputTextView.Text = "";
        clearButton.Enabled = false;
        classifyButton.Enabled = false;
    }

    [Export("expandButtonTouchUpInside:")]
    public void ExpandButtonTouchUpInside(UIButton sender)
    {
        sender.Selected = !sender.Selected;
        settingViewHeightLayoutConstraint.Constant = sender.Selected ? 160 : 80;
        UIView.Animate(duration: 0.3, () =>
            this.View?.LayoutSubviews());
    }

    // Private function
    private void SetupUI()
    {
        NavigationItem.TitleView = titleView;

        // Chose model option
        UIActionHandler choseModel = (UIAction action) =>
            this.Update(modelTitle: action.Title);
        var actions = Enum.GetValues<Model>().Select((model, _) =>
        {
            var action = UIAction.Create(title: model.ToText(),
                image: null, identifier: null, handler: choseModel);
            if (model == Constants.DefaultModel)
            {
                action.State = UIMenuElementState.On;
            }
            return action;
        }).ToArray();
        chooseModelButton.Menu = UIMenu.Create(children: actions);
        chooseModelButton.ShowsMenuAsPrimaryAction = true;
        chooseModelButton.ChangesSelectionAsPrimaryAction = true;

        inputTextView.Text = Constants.DefaultText;
        inputTextView.Layer.BorderColor = UIColor.Black.CGColor;
        inputTextView.Layer.BorderWidth = 1;
    }

    private void SetupTableView()
    {
        tableView.RegisterClassForCellReuse(typeof(UITableViewCell), reuseIdentifier: "DefaultCell");
        tableView.DataSource = this;
        tableView.RowHeight = 44;
    }

    private void HideKeyboardWhenTappedAround()
    {
        var tap = new UITapGestureRecognizer(target: this, new ObjCRuntime.Selector("dismissKeyboard"));
        tap.CancelsTouchesInView = false;
        View?.AddGestureRecognizer(tap);
    }

    [Export("dismissKeyboard")]
    private void DismissKeyboard()
    {
        View?.EndEditing(true);
    }

    private void Update(string modelTitle)
    {
        Model model = modelTitle.ToModel();
        textClassifier = new TextClassifierHelper(model: model);
    }
}

public partial class ViewController : IUITableViewDataSource
{
    public nint RowsInSection(UITableView tableView, nint section)
    {
        return categories != null ? categories.Length : 0;
    }

    public UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
    {
        var cell = tableView.DequeueReusableCell(identifier: "DefaultCell");
        var category = categories[indexPath.Row];
        var text = category.CategoryName + " (" + category.Score + ")";
#pragma warning disable CA1422
        cell.TextLabel.Text = text;
#pragma warning restore
        return cell;
    }
}

public partial class ViewController : IUITextViewDelegate
{
    [Export("textViewDidChange:")]
    public void Changed(UITextView textView)
    {
        clearButton.Enabled = textView.Text?.Length != 0;
        classifyButton.Enabled = textView.Text?.Length != 0;
    }

    public struct Constants
    {
        public const string DefaultText = "Google has released 24 versions of the Android operating system since 2008 and continues to make substantial investments to develop, grow, and improve the OS.";
        public const Model DefaultModel = Model.MobileBert;
    }
}
