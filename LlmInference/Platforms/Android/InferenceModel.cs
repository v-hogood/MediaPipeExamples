using Android.Content;
using Android.Util;
using Google.Common.Util.Concurrent;
using MediaPipe.Tasks.GenAI.LlmInference;
using static MediaPipe.Tasks.GenAI.LlmInference.LlmInference;
using Exception = Java.Lang.Exception;
using File = Java.IO.File;
using IllegalArgumentException = Java.Lang.IllegalArgumentException;
using LlmInferenceSessionOptions = MediaPipe.Tasks.GenAI.LlmInference.LlmInferenceSession.LlmInferenceSessionOptions;
using Uri = Android.Net.Uri;

namespace LlmInference;

class ModelLoadFailException : Exception
{
    public ModelLoadFailException() :
        base("Failed to load model, please try again") {}
}

class ModelSessionCreateFailException : Exception
{
    public ModelSessionCreateFailException() :
        base("Failed to create model session, please try again") {}
}

public class InferenceModel
{
    // The maximum number of tokens the model can process.
    public const int MaxTokens = 1024;
    //
    // An offset in tokens that we use to ensure that the model always has the ability to respond when
    // we compute the remaining context length.
    //
    public const int DecodeTokenOffset = 256;

    private MediaPipe.Tasks.GenAI.LlmInference.LlmInference llmInference;
    private LlmInferenceSession llmInferenceSession;
    private static readonly string Tag = typeof(InferenceModel).Name;

    public ChatUiState UiState => new(Model.Thinking, new List<ChatMessage>());

    private InferenceModel(Context context)
    {
        if (!ModelExists(context))
        {
            throw new IllegalArgumentException("Model not found at path: " + Model.Path);
        }

        CreateEngine(context);
        CreateSession();
    }

    public void Close()
    {
        llmInferenceSession?.Close();
        llmInference?.Close();
     }

    public void ResetSession()
    {
        llmInferenceSession?.Close();
        CreateSession();
    }

    private void CreateEngine(Context context)
    {
        var builder = LlmInferenceOptions.InvokeBuilder()
            .SetModelPath(ModelPath(context))
            .SetMaxTokens(MaxTokens);
        if (Model.PreferredBackend != null)
        {
            builder.SetPreferredBackend(Model.PreferredBackend);
        }
        var inferenceOptions = builder.Build();

        try
        {
            llmInference = CreateFromOptions(context, inferenceOptions);
        }
        catch (Exception e)
        {
            Log.Error(Tag, "Load model error: " + e.Message, e);
            throw new ModelLoadFailException();
        }
    }

    private void CreateSession()
    {
        var sessionOptions = LlmInferenceSessionOptions.InvokeBuilder()
            .SetTemperature(Model.Temperature)
            .SetTopK(Model.TopK)
            .SetTopP(Model.TopP)
            .Build();

        try
        {
            llmInferenceSession =
                LlmInferenceSession.CreateFromOptions(llmInference, sessionOptions);
        }
        catch (Exception e)
        {
            Log.Error(Tag, "LlmInferenceSession create error: " + e.Message, e);
            throw new ModelSessionCreateFailException();
        }
    }

    public IListenableFuture GenerateResponseAsync(string prompt, IProgressListener progressListener)
    {
        llmInferenceSession.AddQueryChunk(prompt);
        return llmInferenceSession.GenerateResponseAsync(progressListener);
    }

    public int EstimateTokensRemaining(string prompt)
    {
        var messages = UiState.Messages.Select(m => m.RawMessage);
        var contextString = String.Join(" ", messages) + prompt;
        if (string.IsNullOrEmpty(contextString)) return -1; // Special marker if no content has been added

        var sizeOfAllMessages = llmInferenceSession.SizeInTokens(contextString);
        var approximateControlTokens = UiState.Messages.Count * 3;
        var remainingTokens = MaxTokens - sizeOfAllMessages - approximateControlTokens - DecodeTokenOffset;
        // Token size is approximate so, let's not return anything below 0
        return Math.Max(0, remainingTokens);
    }

    public static Model Model = Model.GEMMA_3_1B_IT_GPU;
    private static InferenceModel instance = null;

    public static InferenceModel GetInstance(Context context)
    {
        if (instance != null)
        {
            return instance;
        }
        else
        {
            return instance = new InferenceModel(context);
        }
    }

    public static InferenceModel ResetInstance(Context context)
    {
        return instance = new InferenceModel(context);
    }

    public static string ModelPathFromUrl(Context context)
    {
        if (!string.IsNullOrEmpty(Model.Url))
        {
            var urlFileName = Uri.Parse(Model.Url).LastPathSegment;
            if (!string.IsNullOrEmpty(urlFileName))
            {
                return new File(context.FilesDir, urlFileName).AbsolutePath;
            }
        }
        
        return "";
    }

    public static string ModelPath(Context context)
    {
        var modelFile = new File(Model.Path);
        if (modelFile.Exists())
        {
            return Model.Path;
        }

        return ModelPathFromUrl(context);
    }

    public static bool ModelExists(Context context)
    {
        return new File(ModelPath(context)).Exists();
    }
}
