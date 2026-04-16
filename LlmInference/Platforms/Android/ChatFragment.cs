using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;
using AndroidX.Lifecycle;
using Google.Android.Material.Card;
using Google.Android.Material.ProgressIndicator;
using Kotlin.Coroutines;
using Kotlin.Jvm.Functions;
using Xamarin.KotlinX.Coroutines;
using Xamarin.KotlinX.Coroutines.Flow;
using static AndroidX.Lifecycle.LifecycleOwnerKt;
using static Xamarin.KotlinX.Coroutines.Flow.FlowKt;
using Boolean = Java.Lang.Boolean;
using Class = Java.Lang.Class;
using Fragment = AndroidX.Fragment.App.Fragment;
using ImageButton = Android.Widget.ImageButton;
using Integer = Java.Lang.Integer;
using Object = Java.Lang.Object;
using ScrollView = Android.Widget.ScrollView;
using View = Android.Views.View;

namespace LlmInference;

public class ChatFragment : Fragment,
    View.IOnClickListener,
    ITextWatcher,
    IFunction3,
    IFlowCollector
{
    public Action OnClose;

    ChatViewModel viewModel;

    private ScrollView chatScrollView;
    private LinearLayout chatContainer;
    private EditText inputMessage;
    private ImageButton sendButton;
    private ImageButton refreshButton;
    private ImageButton closeButton;
    private TextView tokensRemaining;
    private TextView modelName;
    private TextView contextFullWarning;

    override public View OnCreateView(
        LayoutInflater inflater,
        ViewGroup container,
        Bundle savedInstanceState)
    {
        return inflater.Inflate(Resource.Layout.fragment_chat, container, false);
    }

    override public void OnViewCreated(View view, Bundle savedInstanceState)
    {
        base.OnViewCreated(view, savedInstanceState);

        chatScrollView = view.FindViewById<ScrollView>(Resource.Id.chat_scroll_view);
        chatContainer = view.FindViewById<LinearLayout>(Resource.Id.chat_container);
        inputMessage = view.FindViewById<EditText>(Resource.Id.input_message);
        sendButton = view.FindViewById<ImageButton>(Resource.Id.btn_send);
        refreshButton = view.FindViewById<ImageButton>(Resource.Id.btn_refresh);
        closeButton = view.FindViewById<ImageButton>(Resource.Id.btn_close);
        tokensRemaining = view.FindViewById<TextView>(Resource.Id.tokens_remaining);
        modelName = view.FindViewById<TextView>(Resource.Id.model_name);
        contextFullWarning = view.FindViewById<TextView>(Resource.Id.context_full_warning);

        modelName.Text = InferenceModel.Model.ToString();

        viewModel = new ViewModelProvider(RequireActivity(),
            new ChatViewModelFactory(RequireContext().ApplicationContext)).
                Get(Class.FromType(typeof(ChatViewModel))) as ChatViewModel;

        viewModel.ResetInferenceModel(InferenceModel.GetInstance(RequireContext().ApplicationContext));
        
        GetLifecycleScope(ViewLifecycleOwner).Launch(() =>
        {
            Collect(Combine(viewModel.UiState, viewModel.TextInputEnabled, this),
                new Continuation());
        });
        
        GetLifecycleScope(ViewLifecycleOwner).Launch(() =>
        {
            Collect(viewModel.TokensRemaining, new Continuation());
        });

        inputMessage.AddTextChangedListener(this);

        sendButton.SetOnClickListener(this);

        refreshButton.SetOnClickListener(this);

        closeButton.SetOnClickListener(this);
    }

    public Object Invoke(Object p0, Object p1, Object p2)
    {
        return new Kotlin.Pair(p0, p1);
    }

    public Object Emit(Object value, IContinuation p1)
    {
        if (value is Kotlin.Pair)
        {
            var pair = value as Kotlin.Pair;
            var uiState = pair.Component1() as ChatUiState;
            var enabled = pair.Component2() as Boolean;

            UpdateChatList(uiState.Messages);
            inputMessage.Enabled = enabled.BooleanValue();
            refreshButton.Enabled = enabled.BooleanValue();
            closeButton.Enabled = enabled.BooleanValue();
                    
            uiState.OnMessagesChanged = new(() =>
            {
                GetLifecycleScope(ViewLifecycleOwner).Launch(Dispatchers.Main, () =>
                {
                    UpdateChatList(uiState.Messages);
                });
            });
        }
        else if (value is Integer)
        {
            var tokens = (value as Integer).IntValue();
            tokensRemaining.Text = tokens >= 0 ? tokens + " " + Resource.String.tokens_remaining : "";
            contextFullWarning.Visibility = tokens == 0 ? ViewStates.Visible : ViewStates.Gone;
            sendButton.Enabled = tokens > 0 && !string.IsNullOrWhiteSpace(inputMessage.Text);
        }

        return null;
    }

    public void BeforeTextChanged(Java.Lang.ICharSequence s, int start, int count, int after) { }

    public void OnTextChanged(Java.Lang.ICharSequence s, int start, int before, int count)
    {
        var text = s.ToString();
        if (!text.Contains(" ") || text.Trim() != text)
        {
            viewModel.RecomputeSizeInTokens(text);
        }
        sendButton.Enabled = !string.IsNullOrWhiteSpace(text) &&
            (viewModel.TokensRemaining.Value as Integer).IntValue() > 0;
    }

    public void AfterTextChanged(IEditable s) { }

    public void OnClick(View v)
    {
        if (v.Id == Resource.Id.btn_send)
        {
            var message = inputMessage.Text;
            viewModel.SendMessage(message);
            inputMessage.Text = "";
        }
        else if (v.Id == Resource.Id.btn_refresh)
        {
            InferenceModel.GetInstance(RequireContext().ApplicationContext).ResetSession();
            (viewModel.UiState.Value as ChatUiState).ClearMessages();
            viewModel.RecomputeSizeInTokens("");
            UpdateChatList(new List<ChatMessage>());
        }
        else if (v.Id == Resource.Id.btn_close)
        {
            InferenceModel.GetInstance(RequireContext().ApplicationContext).ResetSession();
            (viewModel.UiState.Value as ChatUiState).ClearMessages();
            viewModel.RecomputeSizeInTokens("");
            OnClose?.Invoke();
        }   
    }

    private void UpdateChatList(List<ChatMessage> messages)
    {
        chatContainer.RemoveAllViews();
        foreach (var chat in messages)
        {
            var chatItem = LayoutInflater.From(RequireContext())
                .Inflate(Resource.Layout.item_chat, chatContainer, false);
            
            var author = chatItem.FindViewById<TextView>(Resource.Id.chat_author);
            var text = chatItem.FindViewById<TextView>(Resource.Id.chat_text);
            var card = chatItem.FindViewById<MaterialCardView>(Resource.Id.chat_card);
            var progress = chatItem.FindViewById<CircularProgressIndicator>(Resource.Id.chat_progress);

            author.Text =
                chat.IsFromUser ? GetString(Resource.String.user_label) :
                chat.IsThinking ? GetString(Resource.String.thinking_label) :
                GetString(Resource.String.model_label);
            
            text.Text = chat.Message;
            var isGenerating = chat.IsLoading && chat.IsEmpty;
            progress.Visibility = isGenerating ? ViewStates.Visible : ViewStates.Gone;
            text.Visibility = isGenerating ? ViewStates.Gone : ViewStates.Visible;

            var authorParams = author.LayoutParameters as LinearLayout.LayoutParams;
            authorParams.Gravity = chat.IsFromUser ? GravityFlags.End : GravityFlags.Start;
            author.LayoutParameters = authorParams;

            var cardParams = card.LayoutParameters as LinearLayout.LayoutParams;
            cardParams.Gravity = chat.IsFromUser ? GravityFlags.End : GravityFlags.Start;
            card.LayoutParameters = cardParams;

            var backgroundColor = chat.IsFromUser ? RequireContext().GetColor(Resource.Color.purple_200) :
                                  chat.IsThinking ? RequireContext().GetColor(Resource.Color.teal_200) :
                                  RequireContext().GetColor(Resource.Color.teal_700);
            card.SetCardBackgroundColor(backgroundColor);
            
            chatContainer.AddView(chatItem, 0); // Add at top since list is reversed in UiState
        }
        chatScrollView.Post(() =>
        {
            chatScrollView.FullScroll(FocusSearchDirection.Down);
        });
    }
}
