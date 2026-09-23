using CoreFoundation;
using Foundation;

namespace LlmInference;

public class ConversationViewModel
{
    private List<MessageViewModel> messageViewModels = new();
    public List<MessageViewModel> MessageViewModels
    {
        get => messageViewModels;
        private set
        {
            messageViewModels = value;
            OnMessageViewModelsChanged?.Invoke(messageViewModels.LastOrDefault());
        }
    }

    private ConversationState currentState = new ConversationState.Idle();
    public ConversationState CurrentState
    {
        get => currentState;
        private set
        {
            currentState = value;
            OnCurrentStateChanged?.Invoke(value);
        }
    }

    private bool downloadRequired = true;
    public bool DownloadRequired
    {
        get => downloadRequired;
        private set
        {
            downloadRequired = value;
            OnDownloadRequiredChanged?.Invoke(value);
        }
    }

    private int remainingSizeInTokens = -1;
    public int RemainingSizeInTokens
    {
        get => remainingSizeInTokens;
        private set
        {
            remainingSizeInTokens = value;
            OnRemainingSizeInTokensChanged?.Invoke(value);
        }
    }

    public Action<MessageViewModel> OnMessageViewModelsChanged;
    public Action<ConversationState> OnCurrentStateChanged;
    public Action<bool> OnDownloadRequiredChanged;
    public Action<int> OnRemainingSizeInTokensChanged;

    public abstract record ConversationState
    {
        public sealed record Idle : ConversationState;
        public sealed record LoadingModel : ConversationState;
        public sealed record PromptSubmitted : ConversationState;
        public sealed record StreamingResponse : ConversationState;
        public sealed record CriticalError(InferenceError Error) : ConversationState;
        public sealed record NonCriticalError(InferenceError Error) : ConversationState;
        public sealed record Done : ConversationState;

        public InferenceError? InferenceError => this switch
        {
            CriticalError error => error.Error,
            NonCriticalError error => error.Error,
            _ => null
        };
    }

    public Model ModelCategory;
    private OnDeviceModel model;
    private Chat chat;

    public ConversationViewModel(Model modelCategory)
    {
        this.ModelCategory = modelCategory;
        downloadRequired = modelCategory.ModelPath == null;
    }

    public void LoadModel()
    {
        if (currentState is ConversationState.Idle && !downloadRequired)
            return;

        currentState = new ConversationState.LoadingModel();
        Task.Run(async () =>
        {
            try
            {
                var loadedModel = new OnDeviceModel(model: ModelCategory);
                model = loadedModel;
                StartNewChat();
            }
            catch (Exception error)
            {
                currentState = new ConversationState.CriticalError(new InferenceError.MediaPipeTasksError(error));
            }
        });
    }

    public void ClearModel()
    {
        chat = null;
        model = null;
        currentState = new ConversationState.LoadingModel();
    }

    public void HandleModelDownloadedCompleted()
    {
        DownloadRequired = false;
        currentState = new ConversationState.Idle();
        LoadModel();
    }

    public void SendMessage(string text)
    {
        Task.Run(async () => await InternalSendMessage(text));
    }

    public void StartNewChat()
    {
        if (model == null)
        {
            CurrentState = new ConversationState.CriticalError(InferenceError.OnDeviceModelNotInitialized.Instance);
            return;
        }

        CurrentState = new ConversationState.LoadingModel();

        try
        {
            chat = new Chat(model: model);
            messageViewModels.Clear();
            CurrentState = new ConversationState.Done();
            RemainingSizeInTokens = -1;
        }
        catch (Exception error)
        {
            CurrentState = new ConversationState.NonCriticalError(new InferenceError.MediaPipeTasksError(error));
        }
    }

    public void ResetStateAfterErrorIntimation()
    {
        if (currentState is ConversationState.NonCriticalError && chat != null)
        {
            CurrentState = new ConversationState.Done();
        }
    }

    private bool ShouldDisableClicks()
    {
        return ShouldDisableClicksForStartNewChat() || remainingSizeInTokens == 0;
    }

    public bool ShouldDisableClicksForStartNewChat()
    {
        if (currentState is ConversationState.Done)
        {
            return false;
        }
        return true;
    }

    public void RecomputeSizeInTokens(string prompt)
    {
        var history = string.Concat(messageViewModels.Select(vm => vm.ChatMessage.Text));
        remainingSizeInTokens =
            chat?.EstimateTokensRemaining(prompt: prompt, history: history, historyCount: messageViewModels.Count)
            ?? remainingSizeInTokens;
    }

    private async Task InternalSendMessage(string text)
    {
        if (chat == null)
        {
            CurrentState = new ConversationState.CriticalError(InferenceError.OnDeviceModelNotInitialized.Instance);
            return;
        }

        CurrentState = new ConversationState.PromptSubmitted();

        CurrentState = remainingSizeInTokens == 0
            ? new ConversationState.NonCriticalError(InferenceError.TokensExceeded.Instance)
            : new ConversationState.Done();

        var userViewModel = new MessageViewModel(chatMessage: new ChatMessage(text: text, participant: new ChatMessage.ParticipantEnum.User()));
        var systemViewModel = new MessageViewModel(
            chatMessage: new ChatMessage(
                participant: ModelCategory.Thinking
                    ? new ChatMessage.ParticipantEnum.System(ChatMessage.ParticipantEnum.SystemEnum.Thinking)
                    : new ChatMessage.ParticipantEnum.System(ChatMessage.ParticipantEnum.SystemEnum.Response),
                isLoading: true));

        messageViewModels.Add(userViewModel);
        messageViewModels.Add(systemViewModel);

        try
        {
            var responseStream = chat.SendMessageAsync(text);
            await UpdateSystemViewModel(systemViewModel, responseStream);
            RecomputeSizeInTokens(prompt: string.Empty);
        }
        catch (Exception error)
        {
            HandleStreamError(error, systemViewModel);
        }
    }

    private async Task UpdateSystemViewModel(MessageViewModel messageVm, IAsyncEnumerable<string> responseStream)
    {
        CurrentState = new ConversationState.Done();
        CurrentState = new ConversationState.StreamingResponse();

        var currentMessageVm = messageVm;

        try
        {
            await foreach (var partialResult in responseStream)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    currentMessageVm = AppendPartialResult(partialResult, currentMessageVm);
                });
            }
        }
        catch (Exception error)
        {
            HandleStreamError(error, messageVm);
        }
    }

    private MessageViewModel AppendPartialResult(string partialResult, MessageViewModel messageVm)
    {
        var newMessageVm = messageVm;

        var result = partialResult.ExtractPrefixSuffix(Model.ThinkingMarkerEnd);
        if (result is { } match)
        {
            var prefix = match.Prefix;
            var suffix = match.Suffix;
            var trimmedSuffix = TrimmedOfMarkers(suffix);

            messageVm.Update(text: prefix, participant: new ChatMessage.ParticipantEnum.System(ChatMessage.ParticipantEnum.SystemEnum.Thinking));

            if (string.IsNullOrEmpty(messageVm.ChatMessage.Text))
            {
                messageVm.Update(text: trimmedSuffix, participant: new ChatMessage.ParticipantEnum.System(ChatMessage.ParticipantEnum.SystemEnum.Response));
            }
            else
            {
                var nextMessageVm = new MessageViewModel(
                    chatMessage: new ChatMessage(text: trimmedSuffix, participant: new ChatMessage.ParticipantEnum.System(ChatMessage.ParticipantEnum.SystemEnum.Response)));

                newMessageVm = nextMessageVm;
                messageViewModels.Add(nextMessageVm);
            }
        }
        else
        {
            messageVm.Update(text: TrimmedOfMarkers(partialResult));
        }

        return newMessageVm;
    }

    private string TrimmedOfMarkers(string text)
    {
        return ModelCategory.Thinking && !string.IsNullOrEmpty(Model.ThinkingMarkerEnd)
            ? text.Replace(Model.ThinkingMarkerEnd, string.Empty)
            : text;
    }

    private void HandleStreamError(Exception error, MessageViewModel messageVm)
    {
        var participant = messageVm.ChatMessage.Participant;

        switch (participant)
        {
            case ChatMessage.ParticipantEnum.System { Value: ChatMessage.ParticipantEnum.SystemEnum.Error }:
                messageViewModels.Add(new MessageViewModel(chatMessage: new ChatMessage(participant: new ChatMessage.ParticipantEnum.System(ChatMessage.ParticipantEnum.SystemEnum.Error))));
                break;

            case ChatMessage.ParticipantEnum.System { Value: ChatMessage.ParticipantEnum.SystemEnum.Thinking }:
            case ChatMessage.ParticipantEnum.System { Value: ChatMessage.ParticipantEnum.SystemEnum.Response }:
                if (!string.IsNullOrEmpty(messageVm.ChatMessage.Text))
                {
                    messageViewModels.Add(new MessageViewModel(chatMessage: new ChatMessage(participant: new ChatMessage.ParticipantEnum.System(ChatMessage.ParticipantEnum.SystemEnum.Error))));
                }
                else
                {
                    messageVm.Update(text: error.Message, participant: new ChatMessage.ParticipantEnum.System(ChatMessage.ParticipantEnum.SystemEnum.Error));
                }
                break;

            case ChatMessage.ParticipantEnum.User:
                break;
        }
    }

    private void RunOnMain(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
        }
        else
        {
            DispatchQueue.MainQueue.DispatchAsync(action);
        }
    }

    public abstract record InferenceError
    {
        public abstract string ErrorDescription { get; }
        public abstract string FailureReason { get; }

        public sealed record MediaPipeTasksError(Exception Error) : InferenceError
        {
            public override string ErrorDescription => "Internal error";
            public override string FailureReason => Error?.Message ?? "Unknown error";
        }

        public sealed record ModelFileNotFound(string ModelName) : InferenceError
        {
            public override string ErrorDescription => "Model not found";
            public override string FailureReason => $"Model with name {ModelName} not found on the disk.";
        }

        public sealed record OnDeviceModelNotInitialized : InferenceError
        {
            public static readonly OnDeviceModelNotInitialized Instance = new();

            public override string ErrorDescription => "Model uninitialized";
            public override string FailureReason => "A valid on device model has not been initialized.";
        }

        public sealed record TokensExceeded : InferenceError
        {
            public static readonly TokensExceeded Instance = new();

            public override string ErrorDescription => "Token limit exceeded";
            public override string FailureReason =>
                "You have exhausted your token limit for the current session. Please refresh the session.";
        }
    }
}

public static class StringExtensions
{
    public static (string Prefix, string Suffix)? ExtractPrefixSuffix(this string text, string substring)
    {
        if (string.IsNullOrEmpty(substring))
            return null;

        var index = text.IndexOf(substring, StringComparison.Ordinal);
        if (index < 0)
            return null;

        var prefix = text[..index];
        var suffix = text[(index + substring.Length)..];

        return (prefix, suffix);
    }
}
