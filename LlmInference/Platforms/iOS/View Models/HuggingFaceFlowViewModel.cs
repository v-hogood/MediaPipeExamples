namespace LlmInference;

public class HuggingFaceFlowViewModel
{
    public enum ActionEnum
    {
        AcknowledgeLicense,
        Download
    }

    ActionEnum action;
    public ActionEnum Action
    {
        get => action;
        set
        {
            action = value;
            OnActionChanged?.Invoke(action);
        }
    }
  
    public Action<ActionEnum> OnActionChanged;
    
    public Model ModelCategory;

    public HuggingFaceFlowViewModel(Model modelCategory)
    {
        this.ModelCategory = modelCategory;
        this.action = HuggingFaceFlowViewModel.NewAction(modelCategory: modelCategory);
    }

    public void UpdateState()
    {
        Action = HuggingFaceFlowViewModel.NewAction(modelCategory: this.ModelCategory);
    }

    static ActionEnum NewAction(Model modelCategory)
    {
        if (!string.IsNullOrEmpty(modelCategory.LicenseAcknowledgedKey) &&
            KeychainHelper.Load(key: modelCategory.LicenseAcknowledgedKey) == null)
        {
            return ActionEnum.AcknowledgeLicense;
        }
        else
        {
            return ActionEnum.Download;
        }
    }
}
