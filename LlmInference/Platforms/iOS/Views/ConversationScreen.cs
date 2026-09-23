using Foundation;
using ObjCRuntime;
using UIKit;

namespace LlmInference;

public class ConversationViewController : UIViewController, IUITableViewDataSource, IUITableViewDelegate, IUITextFieldDelegate
{
    private UIColor alertBackgroundColor = UIColor.Black.ColorWithAlpha(0.3f);
    private const string newChatSystemSymbolName = "arrow.clockwise";
    private const string modelInitializationAlertText = "Model initialization in progress.";

    private readonly ConversationViewModel viewModel;

    // UI Components
    private readonly UITableView tableView = new();
    private readonly UIView tokenAccessoryView = new();
    private readonly UILabel tokenLabel = new();
    
    private readonly UIView loadingOverlay = new();
    private readonly UIActivityIndicatorView activityIndicator = new(UIActivityIndicatorViewStyle.Large);
    private readonly UILabel loadingLabel = new();

    private readonly UIView typingView = new();
    private readonly UITextField textField = new();
    private readonly UIButton sendButton = new(UIButtonType.System);

    public ConversationViewController(ConversationViewModel viewModel)
    {
        this.viewModel = viewModel;
    }

    public ConversationViewController(IntPtr handle) : base(handle) { }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        View.BackgroundColor = UIColor.SystemBackground;
        SetupNavigationBar();
        SetupTableView();
        SetupTokenAccessoryView();
        SetupTypingView();
        SetupLoadingOverlay();

        BindViewModel();

        viewModel.LoadModel();
    }

    public override void ViewWillDisappear(bool animated)
    {
        base.ViewWillDisappear(animated);
        if (base.IsMovingFromParentViewController || IsBeingDismissed)
        {
            viewModel.ClearModel();
        }
    }

    private void SetupNavigationBar()
    {
        Title = $"Chat with {viewModel.ModelCategory.Name}";
        var refreshButton = new UIBarButtonItem(
            image: UIImage.GetSystemImage(name:newChatSystemSymbolName),
            style: UIBarButtonItemStyle.Plain,
            target: this,
            action: new Selector(nameof(DidTapNewChat)));
        NavigationItem.RightBarButtonItem = refreshButton;
    }

    private void SetupTableView()
    {
        tableView.DataSource = this;
        tableView.Delegate = this;
        tableView.SeparatorStyle = UITableViewCellSeparatorStyle.None;
        tableView.AllowsSelection = false;
        tableView.RegisterClassForCellReuse(typeof(MessageCell), "MessageCell");
        tableView.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(tableView);
    }

    private void SetupTokenAccessoryView()
    {
        tokenAccessoryView.BackgroundColor = UIColor.SystemGroupedBackground;
        tokenAccessoryView.TranslatesAutoresizingMaskIntoConstraints = false;

        tokenLabel.Font = UIFont.SystemFontOfSize(14);
        tokenLabel.TextAlignment = UITextAlignment.Center;
        tokenLabel.TranslatesAutoresizingMaskIntoConstraints = false;

        tokenAccessoryView.AddSubview(tokenLabel);
        View.AddSubview(tokenAccessoryView);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            tokenLabel.TopAnchor.ConstraintEqualTo(tokenAccessoryView.TopAnchor, 8),
            tokenLabel.BottomAnchor.ConstraintEqualTo(tokenAccessoryView.BottomAnchor, -8),
            tokenLabel.LeadingAnchor.ConstraintEqualTo(tokenAccessoryView.LeadingAnchor, 16),
            tokenLabel.TrailingAnchor.ConstraintEqualTo(tokenAccessoryView.TrailingAnchor, -16)
        });
    }

    private void SetupTypingView()
    {
        typingView.BackgroundColor = UIColor.SystemGray6;
        typingView.TranslatesAutoresizingMaskIntoConstraints = false;

        textField.Placeholder = "Message...";
        textField.BorderStyle = UITextBorderStyle.RoundedRect;
        textField.BackgroundColor = UIColor.SecondarySystemBackground;
        textField.Delegate = this;
        textField.EditingChanged += (_, _) => TextFieldDidChange();
        textField.TranslatesAutoresizingMaskIntoConstraints = false;
        
        var sendImage = UIImage.GetSystemImage("arrow.up.circle.fill").ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate);
        sendButton.SetImage(sendImage, UIControlState.Normal);
        sendButton.TintColor = Metadata.GlobalColor;
        sendButton.TranslatesAutoresizingMaskIntoConstraints = false;
        sendButton.TouchUpInside += (_, _) => DidTapSend();

        typingView.AddSubview(textField);
        typingView.AddSubview(sendButton);
        View.AddSubview(typingView);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            tokenAccessoryView.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
            tokenAccessoryView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
            tokenAccessoryView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),

            tableView.TopAnchor.ConstraintEqualTo(tokenAccessoryView.BottomAnchor),
            tableView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
            tableView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
            tableView.BottomAnchor.ConstraintEqualTo(typingView.TopAnchor),

            typingView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
            typingView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
            typingView.BottomAnchor.ConstraintEqualTo(View.KeyboardLayoutGuide.TopAnchor),

            textField.TopAnchor.ConstraintEqualTo(typingView.TopAnchor, 10),
            textField.BottomAnchor.ConstraintEqualTo(typingView.SafeAreaLayoutGuide.BottomAnchor, -10),
            textField.LeadingAnchor.ConstraintEqualTo(typingView.LeadingAnchor, 16),
            textField.TrailingAnchor.ConstraintEqualTo(sendButton.LeadingAnchor, -12),

            sendButton.CenterYAnchor.ConstraintEqualTo(textField.CenterYAnchor),
            sendButton.TrailingAnchor.ConstraintEqualTo(typingView.TrailingAnchor, -16),
            sendButton.WidthAnchor.ConstraintEqualTo(36),
            sendButton.HeightAnchor.ConstraintEqualTo(36)
        });
    }

    private void SetupLoadingOverlay()
    {
        loadingOverlay.BackgroundColor = alertBackgroundColor;
        loadingOverlay.Hidden = true;
        loadingOverlay.TranslatesAutoresizingMaskIntoConstraints = false;

        activityIndicator.Color = Metadata.GlobalColor;
        activityIndicator.TranslatesAutoresizingMaskIntoConstraints = false;

        loadingLabel.Text = modelInitializationAlertText;
        loadingLabel.TextColor = UIColor.White;
        loadingLabel.Font = UIFont.BoldSystemFontOfSize(16);
        loadingLabel.TextAlignment = UITextAlignment.Center;
        loadingLabel.TranslatesAutoresizingMaskIntoConstraints = false;

        var container = new UIStackView(views: new UIView[] { activityIndicator, loadingLabel })
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 16,
            Alignment = UIStackViewAlignment.Center,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        loadingOverlay.AddSubview(container);
        View.AddSubview(loadingOverlay);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            loadingOverlay.TopAnchor.ConstraintEqualTo(View.TopAnchor),
            loadingOverlay.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
            loadingOverlay.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
            loadingOverlay.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),

            container.CenterXAnchor.ConstraintEqualTo(loadingOverlay.CenterXAnchor),
            container.CenterYAnchor.ConstraintEqualTo(loadingOverlay.CenterYAnchor)
        });
    }

    private void BindViewModel()
    {
        viewModel.OnMessageViewModelsChanged = _ =>
        {
            tableView.ReloadData();
            ScrollToBottom();
        };
        viewModel.OnCurrentStateChanged = UpdateStateUi;
        viewModel.OnRemainingSizeInTokensChanged = UpdateTokenUi;
        viewModel.OnDownloadRequiredChanged = required =>
        {
            if (required)
                ShowDownloadFlow();
        };

        // Trigger initial settings
        tableView.ReloadData();
        UpdateStateUi(viewModel.CurrentState);
        UpdateTokenUi(viewModel.RemainingSizeInTokens);
        if (viewModel.DownloadRequired)
            ShowDownloadFlow();
    }

    private void UpdateStateUi(ConversationViewModel.ConversationState state)
    {
        NavigationItem.RightBarButtonItem.Enabled = !viewModel.ShouldDisableClicksForStartNewChat();

        var shouldShowLoading = state is ConversationViewModel.ConversationState.LoadingModel;
        loadingOverlay.Hidden = !shouldShowLoading;
        if (shouldShowLoading)
            activityIndicator.StartAnimating();
        else
            activityIndicator.StopAnimating();

        if (state.InferenceError != null)
            ShowErrorAlert(state.InferenceError);
        
        sendButton.Enabled = state is ConversationViewModel.ConversationState.Done && !string.IsNullOrEmpty(textField.Text);
        sendButton.Alpha = sendButton.Enabled ? 1f : 0.4f;
    }

    private void UpdateTokenUi(int tokenCount)
    {
        tokenAccessoryView.Hidden = tokenCount == -1;
        if (tokenCount == 0)
        {
            tokenLabel.Text = "0 tokens remaining. Please refresh the session.";
            tokenLabel.TextColor = UIColor.SystemRed;
        }
        else if (tokenCount > 0)
        {
            tokenLabel.Text = $"{tokenCount} tokens remaining.";
            tokenLabel.TextColor = UIColor.Label;
        }
    }

    private void ShowDownloadFlow()
    {
        var flowViewModel = new HuggingFaceFlowViewModel(modelCategory:viewModel.ModelCategory);
        var flowVC = new HuggingFaceFlowViewController(viewModel: flowViewModel);
        flowVC.ModalPresentationStyle = UIModalPresentationStyle.PageSheet;
        var sheet = flowVC.PresentationController as UISheetPresentationController;
        if (sheet != null)
        {
            sheet.Detents = [UISheetPresentationControllerDetent.CreateMediumDetent(), UISheetPresentationControllerDetent.CreateLargeDetent()];
            sheet.PrefersGrabberVisible = true;
        }
        PresentViewController(flowVC, true, () =>
            viewModel.HandleModelDownloadedCompleted());
    }

    private void ShowErrorAlert(ConversationViewModel.InferenceError error)
    {
        var alert = UIAlertController.Create(title: "Error", message: error.FailureReason, preferredStyle: UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create(title: "OK", style: UIAlertActionStyle.Default, handler: _ =>
        {
            if (viewModel.CurrentState is ConversationViewModel.ConversationState.CriticalError)
                NavigationController?.PopViewController(true);
            else
                viewModel.ResetStateAfterErrorIntimation();
        }));
        PresentViewController(alert, animated:true, null);
    }
    
    [Export(nameof(DidTapNewChat))]
    private void DidTapNewChat()
    {
        viewModel.StartNewChat();
    }

    private void DidTapSend()
    {
        var prompt = textField.Text;
        if (string.IsNullOrEmpty(prompt)) return;
        viewModel.SendMessage(prompt);
        textField.Text = "";
        TextFieldDidChange();
    }

    private void TextFieldDidChange()
    {
        var text = textField.Text ?? "";
        viewModel.RecomputeSizeInTokens(text);
        sendButton.Enabled = viewModel.CurrentState is ConversationViewModel.ConversationState.Done && !string.IsNullOrEmpty(text);
        sendButton.Alpha = sendButton.Enabled ? 1f : 0.4f;
    }

    private void ScrollToBottom()
    {
        var count = viewModel.MessageViewModels.Count;
        if (count <= 0) return;
        var lastIndexPath = NSIndexPath.FromRowSection(row: count - 1, section:0);
        tableView.ScrollToRow(lastIndexPath, UITableViewScrollPosition.Bottom, true);
    }

    // MARK: - UITableViewDataSource
    public nint RowsInSection(UITableView tableView, nint section) => viewModel.MessageViewModels.Count;

    public UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
    {
        var cell = (MessageCell)tableView.DequeueReusableCell("MessageCell", indexPath);
        var messageVM = viewModel.MessageViewModels[indexPath.Row];

        cell.Configure(messageVM: messageVM, onHeightChanged: () =>
        {
            // Disable animations to prevent rapid flickering during streaming layout updates
            UIView.PerformWithoutAnimation(() =>
            {
                tableView.BeginUpdates();
                tableView.EndUpdates();
            });
        });
        ScrollToBottom();

        return cell;
    }

    // MARK: - UITextFieldDelegate
    [Export("textFieldShouldReturn:")]
    public bool ShouldReturn(UITextField textField)
    {
        textField.ResignFirstResponder();
        return true;
    }
}

public class MessageCell : UITableViewCell
{
    private readonly UILabel nameLabel = new();
    private readonly UIView bubbleView = new();
    private readonly UILabel contentLabel = new();
    private readonly UIActivityIndicatorView loadingIndicator = new(UIActivityIndicatorViewStyle.Medium);
    
    private NSLayoutConstraint bubbleLeadingConstraint;
    private NSLayoutConstraint bubbleTrailingConstraint;

    public MessageCell(IntPtr handle) : base(handle) { SetupLayout(); }

    private void SetupLayout()
    {
        SelectionStyle = UITableViewCellSelectionStyle.None;
        nameLabel.Font = UIFont.SystemFontOfSize(10, UIFontWeight.Regular);
        nameLabel.TextColor = UIColor.SecondaryLabel;
        nameLabel.TranslatesAutoresizingMaskIntoConstraints = false;

        bubbleView.Layer.CornerRadius = 16;
        bubbleView.TranslatesAutoresizingMaskIntoConstraints = false;

        contentLabel.Lines = 0;
        contentLabel.Font = UIFont.PreferredBody;
        contentLabel.TranslatesAutoresizingMaskIntoConstraints = false;

        loadingIndicator.TranslatesAutoresizingMaskIntoConstraints = false;

        bubbleView.AddSubview(contentLabel);
        bubbleView.AddSubview(loadingIndicator);
        ContentView.AddSubview(nameLabel);
        ContentView.AddSubview(bubbleView);

        bubbleLeadingConstraint = bubbleView.LeadingAnchor.ConstraintEqualTo(ContentView.LeadingAnchor, 16);
        bubbleTrailingConstraint = bubbleView.TrailingAnchor.ConstraintEqualTo(ContentView.TrailingAnchor, -16);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            nameLabel.TopAnchor.ConstraintEqualTo(ContentView.TopAnchor, 8),
            nameLabel.LeadingAnchor.ConstraintEqualTo(bubbleView.LeadingAnchor, 4),
            nameLabel.TrailingAnchor.ConstraintEqualTo(bubbleView.TrailingAnchor, -4),

            bubbleView.TopAnchor.ConstraintEqualTo(nameLabel.BottomAnchor, 4),
            bubbleView.BottomAnchor.ConstraintEqualTo(ContentView.BottomAnchor, -8),
            bubbleView.WidthAnchor.ConstraintLessThanOrEqualTo(ContentView.WidthAnchor, 0.75f),
            
            contentLabel.TopAnchor.ConstraintEqualTo(bubbleView.TopAnchor, 10),
            contentLabel.BottomAnchor.ConstraintEqualTo(bubbleView.BottomAnchor, -10),
            contentLabel.LeadingAnchor.ConstraintEqualTo(bubbleView.LeadingAnchor, 12),
            contentLabel.TrailingAnchor.ConstraintEqualTo(bubbleView.TrailingAnchor, -12),

            loadingIndicator.CenterXAnchor.ConstraintEqualTo(bubbleView.CenterXAnchor),
            loadingIndicator.CenterYAnchor.ConstraintEqualTo(bubbleView.CenterYAnchor)
        });
    }

    public void Configure(MessageViewModel messageVM, Action onHeightChanged)
    {
        messageVM.OnMessageChanged = message =>
        {
            UpdateUI(message);
            onHeightChanged();
        };
        UpdateUI(messageVM.ChatMessage);
    }

    private void UpdateUI(ChatMessage message)
    {
        nameLabel.Text = message.Title;

        var isUser = message.Participant is ChatMessage.ParticipantEnum.User;
        nameLabel.TextAlignment = isUser ? UITextAlignment.Right : UITextAlignment.Left;

        bubbleLeadingConstraint.Active = !isUser;
        bubbleTrailingConstraint.Active = isUser;

        contentLabel.Hidden = message.IsLoading;
        loadingIndicator.Hidden = !message.IsLoading;
        if (message.IsLoading) loadingIndicator.StartAnimating();
        else loadingIndicator.StopAnimating();

        if (message.Participant is ChatMessage.ParticipantEnum.System { Value: ChatMessage.ParticipantEnum.SystemEnum.Error })
        {
            contentLabel.Text = "Could not generate response";
        }
        else
        {
            contentLabel.Text = message.Text;
        }
 
        switch (message.Participant)
        {
            case ChatMessage.ParticipantEnum.User:
                bubbleView.BackgroundColor = UIColor.SystemBlue;
                contentLabel.TextColor = UIColor.White;
                break;
            case ChatMessage.ParticipantEnum.System { Value: ChatMessage.ParticipantEnum.SystemEnum.Thinking }:
                bubbleView.BackgroundColor = UIColor.SystemGray5;
                contentLabel.TextColor = UIColor.Label;
                break;
            case ChatMessage.ParticipantEnum.System { Value: ChatMessage.ParticipantEnum.SystemEnum.Response }:
                bubbleView.BackgroundColor = UIColor.SystemGray6;
                contentLabel.TextColor = UIColor.Label;
                break;
            default:
                bubbleView.BackgroundColor = UIColor.SystemRed.ColorWithAlpha(0.1f);
                contentLabel.TextColor = UIColor.SystemRed;
                break;
        }
    }
}
