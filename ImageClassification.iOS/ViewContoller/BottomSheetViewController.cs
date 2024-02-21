using MediaPipeTasksVision;
using ObjCRuntime;

namespace ImageClassification;

public interface BottomSheetViewControllerDelegate
{
    //
    // This method is called when the user opens or closes the bottom sheet.
    //
    void ViewController(BottomSheetViewController viewController, bool isOpen);
}

//
// The view controller is responsible for presenting the controls to change the meta data for the image classifier (model, max results,
// score threshold) and updating the singleton`` ClassifierMetadata`` on user input.
//
public partial class BottomSheetViewController : UIViewController
{
    public BottomSheetViewController(NativeHandle handle) : base(handle) { }

    public BottomSheetViewControllerDelegate Delegate;

    private const float normalCellHeight = 27;
    private static MPPImageClassifierResult imageClassifierResult;

    public float CollapsedHeight =>
        normalCellHeight * (float)InferenceConfigManager.SharedInstance.MaxResults;
    bool uiEnabled = false;
    public bool UIEnabled
    {
        get
        {
            return uiEnabled;
        }
        set
        {
            uiEnabled = value;
            EnableOrDisableClicks();
        }
    }
  
    override public void ViewDidLoad()
    {
        base.ViewDidLoad();
        SetupUI();
        EnableOrDisableClicks();
    }
  
    public void Update(string inferenceTimeString, MPPImageClassifierResult result)
    {
        inferenceTimeLabel.Text = inferenceTimeString;
        imageClassifierResult = result;
        tableView.ReloadData();
    }

    private void SetupUI()
    {
        maxResultStepper.Value = (double)(InferenceConfigManager.SharedInstance.MaxResults);
        maxResultLabel.Text = "" + InferenceConfigManager.SharedInstance.MaxResults;

        thresholdStepper.Value = (double)InferenceConfigManager.SharedInstance.ScoreThreshold;
        thresholdValueLabel.Text = "" + InferenceConfigManager.SharedInstance.ScoreThreshold;

        // Chose model option
        UIActionHandler choseModel = (UIAction action) =>
            this.UpdateModel(modelTitle: action.Title);
        var actions = Enum.GetValues<Model>().Select((model, _) =>
        {
            var action = UIAction.Create(title: model.ToText(),
                image: null, identifier: null, choseModel);
            if (model == InferenceConfigManager.SharedInstance.Model)
            {
                action.State = UIMenuElementState.On;
            }
            return action;
        }).ToArray();
        choseModelButton.Menu = UIMenu.Create(children: actions);
        choseModelButton.ShowsMenuAsPrimaryAction = true;
        choseModelButton.ChangesSelectionAsPrimaryAction = true;

        // Setup table view cell height
        tableView.RowHeight = normalCellHeight;
    }

    private void UpdateModel(string modelTitle)
    {
        Model model = modelTitle.ToModel();
        InferenceConfigManager.SharedInstance.Model = model;
    }
  
    private void EnableOrDisableClicks()
    {
        thresholdStepper.Enabled = UIEnabled;
    }

    [Export("expandButtonTouchUpInside:")]
    void ExpandButtonTouchUpInside(UIButton sender)
    {
        sender.Selected = !sender.Selected;
        inferenceTimeLabel.Hidden = !sender.Selected;
        inferenceTimeNameLabel.Hidden = !sender.Selected;
        Delegate?.ViewController(this, isOpen: sender.Selected);
    }

    [Export("thresholdStepperValueChanged:")]
    void ThresholdStepperValueChanged(UIStepper sender)
    {
        var scoreThreshold = (float)sender.Value;
        InferenceConfigManager.SharedInstance.ScoreThreshold = scoreThreshold;
        thresholdValueLabel.Text = "" + scoreThreshold;
    }

    [Export("maxResultStepperValueChanged:")]
    void MaxResultStepperValueChanged(UIStepper sender)
    {
        var maxResults = (int)sender.Value;
        InferenceConfigManager.SharedInstance.MaxResults = maxResults;
        maxResultLabel.Text = "" + maxResults;
    }
}

partial class BottomSheetViewController : IUITableViewDataSource
{
    public nint RowsInSection(UITableView tableView, nint section) =>
        InferenceConfigManager.SharedInstance.MaxResults;

    public UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
    {
        var cell = tableView.DequeueReusableCell(identifier: "INFO_CELL") as InfoCell;
        var classification = imageClassifierResult?.ClassificationResult?.Classifications?.First();
        if (classification == null || classification.Categories.Length <= indexPath.Row)
        {
            cell.fieldNameLabel.Text = "--";
            cell.infoLabel.Text = "--";
            return cell;
        }
        var category = classification.Categories[indexPath.Row];
        cell.fieldNameLabel.Text = category.CategoryName;
        cell.infoLabel.Text = string.Format("{0:0.00}", category.Score);
        return cell;
    }
}
