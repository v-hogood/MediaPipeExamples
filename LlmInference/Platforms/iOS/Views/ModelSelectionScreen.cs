using Foundation;
using UIKit;

namespace LlmInference;

public class ModelSelectionViewController : UITableViewController
{
    private static readonly Model[] Models = typeof(Model)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(field => field.FieldType == typeof(Model))
        .Select(field => (Model)field.GetValue(null))
        .ToArray();

    private const string CellIdentifier = "ModelCell";

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Title = "Models";
        TableView.RegisterClassForCellReuse(typeof(UITableViewCell), CellIdentifier);
        TableView.TableFooterView = new UIView();
    }

    public override nint RowsInSection(UITableView tableView, nint section) =>
        Models.Length;

    public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
    {
        var cell = tableView.DequeueReusableCell(CellIdentifier, indexPath);
        cell.TextLabel.Text = Models[indexPath.Row].Name;
        cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
        return cell;
    }

    public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
    {
        tableView.DeselectRow(indexPath: indexPath, animated: true);
        var selectedModel = Models[indexPath.Row];
        var conversation = new ConversationViewController(new ConversationViewModel(selectedModel));
        NavigationController?.PushViewController(conversation, true);
    }
}
