using Android.Content;
using AndroidX.Lifecycle;
using Kotlin.Coroutines;
using Kotlin.Jvm.Functions;
using MediaPipe.Tasks.GenAI.LlmInference;
using Xamarin.KotlinX.Coroutines;
using Xamarin.KotlinX.Coroutines.Flow;
using static AndroidX.Lifecycle.ViewModelKt;
using static Xamarin.KotlinX.Coroutines.ExecutorsKt;
using static Xamarin.KotlinX.Coroutines.Flow.StateFlowKt;
using Boolean = Java.Lang.Boolean;
using Class = Java.Lang.Class;
using Exception = Java.Lang.Exception;
using Integer = Java.Lang.Integer;
using Object = Java.Lang.Object;
using Runnable = Java.Lang.Runnable;

namespace LlmInference;

public class ChatViewModel : ViewModel,
    IProgressListener
{
    private InferenceModel inferenceModel;

    private IMutableStateFlow uiState = MutableStateFlow(new ChatUiState(false, new List<ChatMessage>()));
    public IStateFlow UiState;
    
    private IMutableStateFlow tokensRemaining = MutableStateFlow(new Integer(-1));
    public IStateFlow TokensRemaining;

    private IMutableStateFlow textInputEnabled = MutableStateFlow(new Boolean(true));
    public IStateFlow TextInputEnabled;

    public ChatViewModel(InferenceModel model)
    {
        inferenceModel = model;
        uiState.Value = model.UiState;

        UiState = uiState;
        TokensRemaining = tokensRemaining;
        TextInputEnabled = textInputEnabled;
    }

    public void ResetInferenceModel(InferenceModel newModel)
    {
        inferenceModel = newModel;
        uiState.Value = inferenceModel.UiState;
    }

    public void SendMessage(string userMessage)
    {
        GetViewModelScope(this).Launch(Dispatchers.IO, () => 
        {
            (uiState.Value as ChatUiState).AddMessage(userMessage, ChatUiState.UserPrefix);
            (uiState.Value as ChatUiState).OnMessagesChanged?.Invoke();
            (uiState.Value as ChatUiState).CreateLoadingMessage();
            (uiState.Value as ChatUiState).OnMessagesChanged?.Invoke();
            SetInputEnabled(false);
            try
            {
                var asyncInference = inferenceModel.GenerateResponseAsync(userMessage, this);
                // Once the inference is done, recompute the remaining size in tokens
                asyncInference.AddListener(new Runnable(() =>
                {
                    GetViewModelScope(this).Launch(Dispatchers.IO, () => 
                    {
                        RecomputeSizeInTokens(userMessage);
                    });
                }), AsExecutor(Dispatchers.Main));
            }
            catch (Exception e)
            {
                (uiState.Value as ChatUiState).AddMessage(e.Message ?? "Unknown Error", ChatUiState.ModelPrefix);
                (uiState.Value as ChatUiState).OnMessagesChanged?.Invoke();
                SetInputEnabled(true);
            }
        });
    }

    public void Run(Object partialResult, bool done)
    {
        (uiState.Value as ChatUiState).AppendMessage(partialResult.ToString());
        (uiState.Value as ChatUiState).OnMessagesChanged?.Invoke();
        if (done)
        {
            SetInputEnabled(true); // Re-enable text input
        }
        else
        {
            // Reduce current token count (estimate only). sizeInTokens() will be used
            // when computation is done
            tokensRemaining.Value = Math.Max(0,
                (tokensRemaining.Value as Integer).IntValue() - 1);
        }
    }

    void SetInputEnabled(bool enabled)
    {
        textInputEnabled.Value = new Boolean(enabled);
    }

    public void RecomputeSizeInTokens(string message)
    {
        var remainingTokens = Math.Max(0, inferenceModel.EstimateTokensRemaining(message));
        tokensRemaining.Value = new Integer(remainingTokens);
    }
}

public class ChatViewModelFactory : Object, ViewModelProvider.IFactory
{
    public ChatViewModelFactory(Context context) =>
        this.context = context;
    private Context context;

    Object ViewModelProvider.IFactory.Create(Class modelClass) =>
        new ChatViewModel(InferenceModel.GetInstance(context));
}

public class Continuation : Object, IContinuation
{
    public ICoroutineContext Context => EmptyCoroutineContext.Instance;

    public void ResumeWith(Object result) { }
}

public static class BuildersKtx
{
    public class Function2 : Object, IFunction2
    {
        Action action;
        public Function2(Action action) => this.action = action;
        public Object Invoke(Object p0, Object p1)
        {
            action();
            return null;
        }
    } 

    public static IJob Launch(this ICoroutineScope scope, Action action) =>
        BuildersKt.Launch(scope, scope.CoroutineContext, CoroutineStart.Default, new Function2(action));

    public static IJob Launch(this ICoroutineScope scope, ICoroutineContext context, Action action) =>
        BuildersKt.Launch(scope, context, CoroutineStart.Default, new Function2(action));

    public static Object WithContext(ICoroutineContext context, Action action) =>
        BuildersKt.WithContext(context, new Function2(action), new Continuation());
}   
