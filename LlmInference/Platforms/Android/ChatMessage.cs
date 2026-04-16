using Java.Util;

namespace LlmInference;

//
// Used to represent a ChatMessage
//
public record ChatMessage
{
    public string Id => UUID.RandomUUID().ToString();
    public string RawMessage = "";
    public string Author = "";
    public bool IsLoading = false;
    public bool IsThinking = false;

    public bool IsEmpty =>
       string.IsNullOrEmpty(RawMessage.Trim());

    public bool IsFromUser =>
        ChatUiState.UserPrefix.Equals(Author);

    public string Message =>
        RawMessage.Trim();
}
