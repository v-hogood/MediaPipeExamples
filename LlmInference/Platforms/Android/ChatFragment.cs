using Android.Content;
using Android.OS;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Lifecycle;
using Google.Android.Material.Card;
using Google.Android.Material.ProgressIndicator;
using Kotlin.Jvm.Functions;
using Xamarin.KotlinX.Coroutines;
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
    IFunction2,
    IFunction3
{
    new private readonly string Tag = typeof(ChatFragment).Name;

    public Action OnClose;

    Context context;
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

        modelName.Text = InferenceModel.Model.Name;

        context = RequireContext().ApplicationContext;
        viewModel = new ViewModelProvider(RequireActivity(),
            new ChatViewModelFactory(context)).
                Get(Class.FromType(typeof(ChatViewModel))) as ChatViewModel;

        // Reset InferenceModel when entering ChatScreen
        var inferenceModel = InferenceModel.GetInstance(context);
        viewModel.ResetInferenceModel(inferenceModel);

        inputMessage.AddTextChangedListener(this);

        sendButton.SetOnClickListener(this);

        refreshButton.SetOnClickListener(this);

        closeButton.SetOnClickListener(this);

        GetLifecycleScope(ViewLifecycleOwner).Launch(() =>
        {
            CollectLatest(viewModel.UiState, this, new Continuation());
        });

        GetLifecycleScope(ViewLifecycleOwner).Launch(() =>
        {
            CollectLatest(viewModel.TextInputEnabled, this, new Continuation());
        });

        GetLifecycleScope(ViewLifecycleOwner).Launch(() =>
        {
            CollectLatest(viewModel.TokensRemaining, this, new Continuation());
        });
    }

    public Object Invoke(Object p0, Object p1, Object p2)
    {
        return new Kotlin.Pair(p0, p1);
    }

    public Object Invoke(Object p0, Object p1)
    {
        if (p0 is ChatUiState)
        {
            var uiState = p0 as ChatUiState;
            Log.Debug(Tag, "messages = " + uiState.Messages.Count);
            uiState.OnMessagesChanged = new(() =>
            {
                GetLifecycleScope(ViewLifecycleOwner).Launch(Dispatchers.Main, () =>
                {
                    UpdateChatList(uiState.Messages);
                });
            });
        }
        else if (p0 is Boolean)
        {
            var enabled = (p0 as Boolean).BooleanValue();
            Log.Debug(Tag, "enabled = " + enabled);
            GetLifecycleScope(ViewLifecycleOwner).Launch(Dispatchers.Main, () =>
            {
                inputMessage.Enabled = enabled;
                refreshButton.Enabled = enabled;
                closeButton.Enabled = enabled;
                sendButton.Enabled = enabled &&
                    (viewModel.TokensRemaining.Value as Integer).IntValue() > 0;
            });
        }
        else if (p0 is Integer)
        {
            var tokens = (p0 as Integer).IntValue();
            Log.Debug(Tag, "tokens = " + tokens);
            GetLifecycleScope(ViewLifecycleOwner).Launch(Dispatchers.Main, () =>
            {
                tokensRemaining.Text = tokens >= 0 ? tokens + " " + GetString(Resource.String.tokens_remaining) : "";
                contextFullWarning.Visibility = tokens == 0 ? ViewStates.Visible : ViewStates.Gone;
                sendButton.Enabled = tokens > 0 &&
                    (viewModel.TextInputEnabled.Value as Boolean).BooleanValue();
            });
        }

        return null;
    }

    public void BeforeTextChanged(Java.Lang.ICharSequence s, int start, int count, int after) { }

    public void OnTextChanged(Java.Lang.ICharSequence s, int start, int before, int count)
    {
        var text = s.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return;
        // Only recompute on first word or when we get a new word
        if (!text.Contains(" ") || text.Trim() != text)
        {
            viewModel.RecomputeSizeInTokens(text);
        }
    }

    public void AfterTextChanged(IEditable s) { }

    public void OnClick(View v)
    {
        if (v.Id == Resource.Id.btn_send)
        {
            var message = inputMessage.Text;
            if (!string.IsNullOrWhiteSpace(message))
            {
                viewModel.SendMessage(message);
                inputMessage.Text = "";                
            }
        }
        else if (v.Id == Resource.Id.btn_refresh)
        {
            InferenceModel.GetInstance(context).ResetSession();
            (viewModel.UiState.Value as ChatUiState).ClearMessages();
            viewModel.RecomputeSizeInTokens("");
            UpdateChatList(new List<ChatMessage>());
        }
        else if (v.Id == Resource.Id.btn_close)
        {
            InferenceModel.GetInstance(context).Close();
            (viewModel.UiState.Value as ChatUiState).ClearMessages();
            viewModel.RecomputeSizeInTokens("");
            OnClose?.Invoke();
        }   
    }

    private void UpdateChatList(List<ChatMessage> messages)
    {
        chatContainer.Post(() =>
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

                var backgroundColor = chat.IsFromUser ? RequireContext().GetColor(Resource.Color.purple_200) :
                                    chat.IsThinking ? RequireContext().GetColor(Resource.Color.teal_200) :
                                    RequireContext().GetColor(Resource.Color.teal_700);
                card.SetCardBackgroundColor(backgroundColor);
                
                chatContainer.AddView(chatItem, 0); // Add at top since list is reversed in UiState
            }
        });
        chatScrollView.Post(() =>
        {
            chatScrollView.FullScroll(FocusSearchDirection.Down);
        });
    }
}
