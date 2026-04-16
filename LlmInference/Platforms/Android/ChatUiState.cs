
namespace LlmInference;

// Management of the message queue.
public class ChatUiState : Java.Lang.Object
{
    public const string UserPrefix = "user";
    public const string ModelPrefix = "model";
    public const string ThinkingMarkerEnd = "</think>";

    private bool supportsThinking = false;
    private List<ChatMessage> messages = new();
    public List<ChatMessage> Messages =>
        messages.AsEnumerable().Reverse().ToList();
    private string currentMessageId = "";

    public Action OnMessagesChanged = null;

    public ChatUiState(bool supportsThinking, List<ChatMessage> messages)
    {
        this.supportsThinking = supportsThinking;
        this.messages = messages.AsEnumerable().Reverse().ToList();
    }

    // Creates a new loading message.
    public void CreateLoadingMessage()
    {
        var chatMessage = new ChatMessage()
        {
            Author = ModelPrefix,
            IsLoading = true,
            IsThinking = supportsThinking
        };
        messages.Add(chatMessage);
        currentMessageId = chatMessage.Id;
    }

     //
     // Appends the specified text to the message with the specified ID.
     // The underlying implementations may split the re-use messages or create new ones. The method
     // always returns the ID of the message used.
     //
    public void AppendMessage(string text)
    {
        var index = messages.FindIndex(it => it.Id.Equals(currentMessageId));

        if (text.Contains(ThinkingMarkerEnd)) // The model is done thinking, we add a new bubble
        {
            var thinkingEnd = text.IndexOf(ThinkingMarkerEnd) + ThinkingMarkerEnd.Length;

            // Add text to current "thinking" bubble
            var prefix = text.Substring(0, thinkingEnd);
            var suffix = text.Substring(thinkingEnd);

            AppendToMessage(currentMessageId, prefix);

            if (messages[index].IsEmpty)
            {
                // There are no thoughts from the model. We can just re-use the current bubble
                messages[index] = messages[index] with
                {
                    IsThinking = false
                };
                AppendToMessage(currentMessageId, suffix);
            }
            else
            {
                // Create a new bubble for the remainder of the model response
                var message = new ChatMessage()
                {
                    RawMessage = suffix,
                    Author = ModelPrefix,
                    IsLoading = true,
                    IsThinking = false
                };
                messages.Add(message);
                currentMessageId = message.Id;
            }
        }
        else
        {
            AppendToMessage(currentMessageId, text);
        }
    }

    private int AppendToMessage(string id, string suffix)
    {
        var index = messages.FindIndex(it => it.Id.Equals(currentMessageId));
        string newText = suffix.Replace(ThinkingMarkerEnd, "");
        messages[index] = messages[index] with
        {
            RawMessage = messages[index].RawMessage + newText,
            IsLoading = false
        };
        return index;
    }

    // Creates a new message with the specified text and author.
    public void AddMessage(string text, string author)
    {
        var chatMessage = new ChatMessage()
        {
            RawMessage = text,
            Author = author,
        };
        messages.Add(chatMessage);
        currentMessageId = chatMessage.Id;
    }

    // Clear all messages.
    public void ClearMessages()
    {
        messages.Clear();
    }
}
