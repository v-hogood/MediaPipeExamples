using Foundation;
using MediaPipeTasksVision;
using ObjCRuntime;
using UIKit;

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
        normalCellHeight * (float)InferenceConfigurationManager.SharedInstance.MaxResults;
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
        maxResultStepper.Value = (double)(InferenceConfigurationManager.SharedInstance.MaxResults);
        maxResultLabel.Text = "" + InferenceConfigurationManager.SharedInstance.MaxResults;

        thresholdStepper.Value = (double)InferenceConfigurationManager.SharedInstance.ScoreThreshold;
        thresholdValueLabel.Text = "" + InferenceConfigurationManager.SharedInstance.ScoreThreshold;

        // Chose model option
        UIActionHandler choseModel = (UIAction action) =>
            this.UpdateModel(modelTitle: action.Title);
        var modelActions = Enum.GetValues<Model>().Select((model, _) =>
        {
            var action = UIAction.Create(title: model.ToString(),
                image: null, identifier: null, choseModel);
            if (model == InferenceConfigurationManager.SharedInstance.Model)
            {
                action.State = UIMenuElementState.On;
            }
            return action;
        }).ToArray();
        chooseModelButton.Menu = UIMenu.Create(children: modelActions);
        chooseModelButton.ShowsMenuAsPrimaryAction = true;
        chooseModelButton.ChangesSelectionAsPrimaryAction = true;

        // Chose delegate option
        UIActionHandler choseDelegate = (UIAction action) =>
            this.UpdateDelegate(delegateTitle: action.Title);
        var delegateActions = Enum.GetValues<ImageClassifierDelegate>().Select((_delegate, _) =>
        {
            var action = UIAction.Create(title: _delegate.ToString(),
                image: null, identifier: null, choseDelegate);
            if (_delegate == InferenceConfigurationManager.SharedInstance.Delegate)
            {
                action.State = UIMenuElementState.On;
            }
            return action;
        }).ToArray();

        chooseDelegateButton.Menu = UIMenu.Create(children: delegateActions);
        chooseDelegateButton.ShowsMenuAsPrimaryAction = true;
        chooseDelegateButton.ChangesSelectionAsPrimaryAction = true;

        // Setup table view cell height
        tableView.RowHeight = normalCellHeight;
    }

    private void UpdateModel(string modelTitle)
    {
        var model = modelTitle.ToModel();
        InferenceConfigurationManager.SharedInstance.Model = model;
    }

    private void UpdateDelegate(string delegateTitle)
    {
        var Delegate = delegateTitle.ToDelegate();
        InferenceConfigurationManager.SharedInstance.Delegate = Delegate;
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
        InferenceConfigurationManager.SharedInstance.ScoreThreshold = scoreThreshold;
        thresholdValueLabel.Text = "" + scoreThreshold;
    }

    [Export("maxResultStepperValueChanged:")]
    void MaxResultStepperValueChanged(UIStepper sender)
    {
        var maxResults = (int)sender.Value;
        InferenceConfigurationManager.SharedInstance.MaxResults = maxResults;
        maxResultLabel.Text = "" + maxResults;
    }
}

partial class BottomSheetViewController : IUITableViewDataSource
{
    public nint RowsInSection(UITableView tableView, nint section) =>
        InferenceConfigurationManager.SharedInstance.MaxResults;

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
