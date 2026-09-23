namespace LlmInference;

#if __ANDROID__
using Backend = MediaPipe.Tasks.GenAI.LlmInference.LlmInference.Backend;
#elif __IOS__
using Foundation;

using Backend = MediaPipeTasksGenAI.LlmPreferredBackend;
#endif

// NB: Make sure the filename is *unique* per model you use!
// Weight caching is currently based on filename alone.
public class Model
{
    public const string ThinkingMarkerEnd = "</think>";

    Model(string name, string path, string url, string licenseUrl,
#if __IOS__
          string licenseAcknowledgedKey,
#endif
          bool needsAuth, Backend preferredBackend, bool thinking, float temperature, int topK, float topP)
    {
        Name = name;
        Path = path;
        Url = url;
        LicenseUrl = licenseUrl;
#if __IOS__
        LicenseAcknowledgedKey = licenseAcknowledgedKey;
#endif
        NeedsAuth = needsAuth;
        PreferredBackend = preferredBackend;
        Thinking = thinking;
        Temperature = temperature;
        TopK = topK;
        TopP = topP;
    }

    public string Name { get; }
    public string Path { get; }
    public string Url { get; }
    public string LicenseUrl { get; }
#if __IOS__
    public string PathName => System.IO.Path.GetFileName(Path);
    public string PathExtension => System.IO.Path.GetExtension(Path).TrimStart('.');
    public string LicenseAcknowledgedKey { get; }
#endif
    public bool NeedsAuth { get; }
    public Backend PreferredBackend { get; }
    public bool Thinking { get; }
    public float Temperature { get; }
    public int TopK { get; }
    public float TopP { get; }

    public static readonly Model GEMMA_3_1B_IT_CPU = new(nameof(GEMMA_3_1B_IT_CPU),
#if __ANDROID__
        "/data/local/tmp/" +
#endif
        "Gemma3-1B-IT_multi-prefill-seq_q8_ekv2048.task",
        "https://huggingface.co/litert-community/Gemma3-1B-IT/resolve/main/Gemma3-1B-IT_multi-prefill-seq_q8_ekv2048.task",
        "https://huggingface.co/litert-community/Gemma3-1B-IT",
#if __IOS__
        "gemma-license",
#endif
        true,
        Backend.Cpu,
        false,
        1.0f,
        64,
        0.95f
    );
    public static readonly Model GEMMA_3_1B_IT_GPU = new(nameof(GEMMA_3_1B_IT_GPU),
#if __ANDROID__
        "/data/local/tmp/" +
#endif
        "Gemma3-1B-IT_multi-prefill-seq_q8_ekv2048.task",
        "https://huggingface.co/litert-community/Gemma3-1B-IT/resolve/main/Gemma3-1B-IT_multi-prefill-seq_q8_ekv2048.task",
        "https://huggingface.co/litert-community/Gemma3-1B-IT",
#if __IOS__
        "gemma-license",
#endif
        true,
        Backend.Gpu,
        false,
        1.0f,
        64,
        0.95f
    );
    public static readonly Model GEMMA_2_2B_IT_CPU = new(nameof(GEMMA_2_2B_IT_CPU),
#if __ANDROID__
        "/data/local/tmp/" +
#endif        
        "Gemma2-2B-IT_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Gemma2-2B-IT/resolve/main/Gemma2-2B-IT_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Gemma2-2B-IT",
#if __IOS__
        "gemma-license",
#endif
        true,
        Backend.Cpu,
        false,
        0.6f,
        50,
        0.9f
    );
    public static readonly Model DEEPSEEK_R1_DISTILL_QWEN_1_5_B = new(nameof(DEEPSEEK_R1_DISTILL_QWEN_1_5_B),
#if __ANDROID__
        "/data/local/tmp/" +
#endif
        "DeepSeek-R1-Distill-Qwen-1.5B_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/DeepSeek-R1-Distill-Qwen-1.5B/resolve/main/DeepSeek-R1-Distill-Qwen-1.5B_multi-prefill-seq_q8_ekv1280.task",
        "",
#if __IOS__
        "",
#endif
        false,
        Backend.Cpu,
        true,
        0.6f,
        40,
        0.7f
    );
    public static readonly Model LLAMA_3_2_1B_INSTRUCT = new(nameof(LLAMA_3_2_1B_INSTRUCT),
#if __ANDROID__
        "/data/local/tmp/" +
#endif
        "Llama-3.2-1B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Llama-3.2-1B-Instruct/resolve/main/Llama-3.2-1B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Llama-3.2-1B-Instruct",
#if __IOS__
        "llama3.2-1B-license",
#endif
        true,
        Backend.Cpu,
        false,
        0.6f,
        64,
        0.9f
    );
    public static readonly Model LLAMA_3_2_3B_INSTRUCT = new(nameof(LLAMA_3_2_3B_INSTRUCT),
#if __ANDROID__
        "/data/local/tmp/" +
#endif
        "Llama-3.2-3B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Llama-3.2-3B-Instruct/resolve/main/Llama-3.2-3B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Llama-3.2-3B-Instruct",
#if __IOS__
        "llama3.2-1B-license",
#endif
        true,
        Backend.Cpu,
        false,
        0.6f,
        64,
        0.9f
    );
    public static readonly Model PHI_4_MINI_INSTRUCT = new(nameof(PHI_4_MINI_INSTRUCT),
#if __ANDROID__
        "/data/local/tmp/" +
#endif
        "Phi-4-mini-instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Phi-4-mini-instruct/resolve/main/Phi-4-mini-instruct_multi-prefill-seq_q8_ekv1280.task",
        "",
#if __IOS__
        "",
#endif
        false,
        Backend.Cpu,
        false,
        0.6f,
        40,
        1.0f
    );
    public static readonly Model QWEN2_0_5B_INSTRUCT = new(nameof(QWEN2_0_5B_INSTRUCT),
#if __ANDROID__
        "/data/local/tmp/" +
#endif
        "Qwen2.5-0.5B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Qwen2.5-0.5B-Instruct/resolve/main/Qwen2.5-0.5B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "",
#if __IOS__
        "",
#endif
        false,
        Backend.Cpu,
        false,
        0.95f,
        40,
        1.0f
    );
    public static readonly Model QWEN2_1_5B_INSTRUCT = new(nameof(QWEN2_1_5B_INSTRUCT),
#if __ANDROID__
        "/data/local/tmp/" +
#endif
        "Qwen2.5-1.5B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Qwen2.5-1.5B-Instruct/resolve/main/Qwen2.5-1.5B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "",
#if __IOS__
        "",
#endif
        false,
        Backend.Cpu,
        false,
        0.95f,
        40,
        1.0f
    );
    public static readonly Model QWEN2_5_3B_INSTRUCT = new(nameof(QWEN2_5_3B_INSTRUCT),
#if __ANDROID__
        "/data/local/tmp/" +
#endif
        "Qwen2.5-3B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Qwen2.5-3B-Instruct/resolve/main/Qwen2.5-3B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "",
#if __IOS__
        "",
#endif
        false,
        Backend.Cpu,
        false,
        0.95f,
        40,
        1.0f
    );
    public static readonly Model SMOLLM_135M_INSTRUCT = new(nameof(SMOLLM_135M_INSTRUCT),
#if __ANDROID__
        "/data/local/tmp/" +
#endif
        "SmolLM-135M-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/SmolLM-135M-Instruct/resolve/main/SmolLM-135M-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "",
#if __IOS__
        "",
#endif
        false,
        Backend.Cpu,
        false,
        0.95f,
        40,
        1.0f
    );
    public static readonly Model TINYLLAMA_1_1B_CHAT_V1_0 = new(nameof(TINYLLAMA_1_1B_CHAT_V1_0),
#if __ANDROID__
        "/data/local/tmp/" +
#endif
        "TinyLlama-1.1B-Chat-v1.0_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/TinyLlama-1.1B-Chat-v1.0/resolve/main/TinyLlama-1.1B-Chat-v1.0_multi-prefill-seq_q8_ekv1280.task",
        "",
#if __IOS__
        "",
#endif
        false,
        Backend.Cpu,
        false,
        0.95f,
        40,
        1.0f
    );

#if __IOS__
    public NSUrl DownloadUrl =>
        NSUrl.FromString(Url);

    public NSUrl LicenseURL =>
        NSUrl.FromString(LicenseUrl);

    public string ModelPath
    {
        get
        {
            var docsUrl = DownloadDestination;
            if (NSFileManager.DefaultManager.FileExists(path: docsUrl.Path))
            {
                return docsUrl.RelativePath;
            }
            var path = NSBundle.MainBundle.PathForResource(PathName, PathExtension);
            if (string.IsNullOrEmpty(path))
            {
                throw new FileNotFoundException($"Model file not found: {Path}");
                // or: throw new InferenceError.ModelFileNotFound(...);
            }

            return path;
        }
    }

    public NSUrl DownloadDestination
    {
        get
        {
            NSError error;
            var path = NSFileManager.DefaultManager.GetUrl(
                directory: NSSearchPathDirectory.DocumentDirectory,
                domain: NSSearchPathDomain.User,
                url: null,
                shouldCreate: true,
                error: out error
            );

            return path;
        }
    }
#endif
}
