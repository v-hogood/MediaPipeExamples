namespace LlmInference;

public interface IIdentifiable<T>
{
    T Id { get; }
}

public class MessageViewModel : IIdentifiable<Guid>
{
    public Guid Id { get; }

    private ChatMessage chatMessage;
    public ChatMessage ChatMessage
    {
        get => chatMessage;
        set
        {
            chatMessage = value;
            var message = chatMessage;
            if (MainThread.IsMainThread)
            {
                OnMessageChanged?.Invoke(message);
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() => OnMessageChanged?.Invoke(message));
            }
        }
    }
    
    public Action<ChatMessage> OnMessageChanged;

    public MessageViewModel(ChatMessage chatMessage)
    {
        this.ChatMessage = chatMessage;
        this.Id = Guid.NewGuid();
    }

    public void Update(ChatMessage.ParticipantEnum participant)
    {
        // Can only update participant of system messages
        if (chatMessage.Participant is ChatMessage.ParticipantEnum.User)
            return;

        // Can only update participant of system message to another system message type
        if (participant is ChatMessage.ParticipantEnum.User)
            return;

        chatMessage.Participant = participant;
    }

    public void Update(string text, ChatMessage.ParticipantEnum participant)
    {
        Update(text: text);
        Update(participant: participant);
    }

    public void Update(string text)
    {
        if (chatMessage.Participant is ChatMessage.ParticipantEnum.User)
            chatMessage.Text = text;
        else
            // Trim any leading characters in whole message.
            chatMessage.Text = (chatMessage.Text + text).TrimStart();
    }
}

// Represents a single message in the chat.
public struct ChatMessage : IIdentifiable<Guid>
{
    // Unique identifier for the message.
    public Guid Id { get; }

    // Text contained in the message.
    string text;
    public string Text
    {
        get => text;
        // didSet gets called whenever the property gets set except for during initialization. Implementation assumes that anyone using
        // chat message will only set to its `text` after it starts receiving model responses.
        set
        {
            text = value;
            IsLoading = false;
        }
    }

    // Indicates if user or system (LLM) has sent the message.
    public ParticipantEnum Participant;
  
    public bool IsLoading { get; private set; }
  
    public string Title =>
        IsLoading && Participant is ParticipantEnum.System { Value: ParticipantEnum.SystemEnum.Response } ? "Generating...." : Participant.Title;

    public ChatMessage(ParticipantEnum participant, string text = "", bool isLoading = false)
    {
        this.text = text;
        this.Participant = participant;
        this.IsLoading = isLoading;
        this.Id = Guid.NewGuid();
    }

    // Represents the type of message.
    public record ParticipantEnum
    {
        public enum SystemEnum
        {
            Thinking,
            Response,
            Error
        }

        public record System(SystemEnum Value) : ParticipantEnum;
        public record User : ParticipantEnum;

        public string DefaultMessage => this switch
        {
            System { Value: SystemEnum.Error } => "Some error occurred.",
            _ => ""
        };

        public string Title => this switch
        {
            System { Value: SystemEnum.Thinking } => "Thinking...",
            System { Value: SystemEnum.Response } or System { Value: SystemEnum.Error } => "Model",
            User => "User",
            _ => ""
        };
    }
}
