using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Foundation;
using MediaPipeTasksGenAI;

namespace LlmInference;

// Represents the LLM that will be used for inference.  It manages a MediaPipe `LlmInference` under the hood.
public class OnDeviceModel
{
    // MediaPipe LlmInference.
    public MPPLLMInference Inference;

    public int MaxTokens = 1024;
    public int DecdodeTokenOffset = 256;

    public Model ModelCategory;

    public OnDeviceModel(Model model)
    {
        ModelCategory = model;
        var options = new MPPLLMInferenceOptions(modelPath: model.ModelPath);
        options.MaxTokens = MaxTokens;

        Inference = new MPPLLMInference(options: options, error: out var error);
    }
}

// Represents a chat session using an instance of `OnDeviceModel`.  It manages a MediaPipe
// `LlmInference.Session` under the hood and passes all response generation queries to the session.
public class Chat
{
    // The on device model using which this chat session was created.
    private OnDeviceModel model;

    // MediaPipe session managed by the current instance.
    private MPPLLMInferenceSession session;

    public Chat(OnDeviceModel model)
    {
        this.model = model;

        var options = new MPPLLMInferenceSessionOptions();
        options.Topk = model.ModelCategory.TopK;
        options.Topp = model.ModelCategory.TopP;
        options.Temperature = model.ModelCategory.Temperature;

        session = new MPPLLMInferenceSession(llmInference: model.Inference, options: options,
            error: out var error);
        if (error != null)
        {
            throw new Exception(error.LocalizedDescription);
        }
    }

    // Sends a streaming response generation query to the underlying MediaPipe
    // `LlmInference.Session`.
    // - Parameters:
    //   - text: Query to the underlying LLM.
    // - Returns: An async throwing stream that contains the partial responses from the LLM.
    // - Throws: A MediaPipe `GenAiInferenceError` if the query cannot be added to the current
    // session.
    public async IAsyncEnumerable<string> SendMessageAsync(
        string text, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        session.AddQueryChunkWithInputText(inputText: text,
            error: out var error);
        if (error != null)
        {
            throw new Exception(error.LocalizedDescription);
        }

        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = true
        });

        session.GenerateResponseAsyncAndReturnError(error: out var generationError,
            progress: (partialResponse, error) =>
            {
                if (error != null)
                {
                    channel.Writer.TryComplete(new Exception(error.LocalizedDescription));
                    return;
                }
                if (partialResponse != null)
                {
                    channel.Writer.TryWrite(partialResponse);
                }
            },
            completion: () =>
            {
                channel.Writer.TryComplete();
            }
        );
        if (generationError != null)
        {
            throw new Exception(generationError.LocalizedDescription);
        }

        while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (channel.Reader.TryRead(out var chunk))
            {
                yield return chunk;
            }
        }
    }

    // Estimates remaining token count that can be processed by the model.
    // - Parameters:
    //   - prompt: User prompt.
    //   - history: Concatenated conversation history.
    //   - historyCount: No: of messages in the history.
    // - Returns: The remaining token count.
    // - Throws: A MediaPipe `GenAiInferenceError` if the token count cannot be estimated.
    public int EstimateTokensRemaining(string prompt, string history, int historyCount)
    {
        var context = $"{history}{prompt}";
        if (string.IsNullOrEmpty(context))
        {
            return -1;
        }

        var messagesTokenCount = (int)session.SizeInTokensWithText(text: context,
            error: out var error);
        if (error != null)
        {
            throw new Exception(error.LocalizedDescription);
        }

        var approximateControlTokensCount = historyCount * 3;
        return Math.Max(
            0,
            model.MaxTokens - model.DecdodeTokenOffset - messagesTokenCount
                - approximateControlTokensCount);
     }
}
