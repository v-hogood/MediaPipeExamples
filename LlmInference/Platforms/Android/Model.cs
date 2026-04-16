namespace LlmInference;

using Backend = MediaPipe.Tasks.GenAI.LlmInference.LlmInference.Backend;

//
// NB: Make sure the filename is *unique* per model you use!
// Weight caching is currently based on filename alone.
//
public class Model
{
    public static readonly Model GEMMA3_1B_IT_CPU = new(
        "/data/local/tmp/Gemma3-1B-IT_multi-prefill-seq_q8_ekv2048.task",
        "https://huggingface.co/litert-community/Gemma3-1B-IT/resolve/main/Gemma3-1B-IT_multi-prefill-seq_q8_ekv2048.task",
        "https://huggingface.co/litert-community/Gemma3-1B-IT",
        true,
        Backend.Cpu,
        false,
        1.0f,
        64,
        0.95f
    );
    public static readonly Model GEMMA_3_1B_IT_GPU = new(
        "/data/local/tmp/Gemma3-1B-IT_multi-prefill-seq_q8_ekv2048.task",
        "https://huggingface.co/litert-community/Gemma3-1B-IT/resolve/main/Gemma3-1B-IT_multi-prefill-seq_q8_ekv2048.task",
        "https://huggingface.co/litert-community/Gemma3-1B-IT",
        true,
        Backend.Gpu,
        false,
        1.0f,
        64,
        0.95f
    );
    public static readonly Model GEMMA_2_2B_IT_CPU = new(
        "/data/local/tmp/Gemma2-2B-IT_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Gemma2-2B-IT/resolve/main/Gemma2-2B-IT_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Gemma2-2B-IT",
        true,
        Backend.Cpu,
        false,
        0.6f,
        50,
        0.9f
    );
    public static readonly Model DEEPSEEK_R1_DISTILL_QWEN_1_5_B = new(
        "/data/local/tmp/DeepSeek-R1-Distill-Qwen-1.5B_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/DeepSeek-R1-Distill-Qwen-1.5B/resolve/main/DeepSeek-R1-Distill-Qwen-1.5B_multi-prefill-seq_q8_ekv1280.task",
        "",
        false,
        Backend.Cpu,
        true,
        0.6f,
        40,
        0.7f
    );
    public static readonly Model LLAMA_3_2_1B_INSTRUCT = new(
        "/data/local/tmp/Llama-3.2-1B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Llama-3.2-1B-Instruct/resolve/main/Llama-3.2-1B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Llama-3.2-1B-Instruct",
        true,
        Backend.Cpu,
        false,
        0.6f,
        64,
        0.9f
    );
    public static readonly Model LLAMA_3_2_3B_INSTRUCT = new(
        "/data/local/tmp/Llama-3.2-3B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Llama-3.2-3B-Instruct/resolve/main/Llama-3.2-3B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Llama-3.2-3B-Instruct",
        true,
        Backend.Cpu,
        false,
        0.6f,
        64,
        0.9f
    );
    public static readonly Model PHI_4_MINI_INSTRUCT = new(
        "/data/local/tmp/Phi-4-mini-instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Phi-4-mini-instruct/resolve/main/Phi-4-mini-instruct_multi-prefill-seq_q8_ekv1280.task",
        "",
        false,
        Backend.Cpu,
        false,
        0.6f,
        40,
        1.0f
    );
    public static readonly Model QWEN2_0_5B_INSTRUCT = new(
        "/data/local/tmp/Qwen2.5-0.5B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Qwen2.5-0.5B-Instruct/resolve/main/Qwen2.5-0.5B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "",
        false,
        Backend.Cpu,
        false,
        0.95f,
        40,
        1.0f
    );
    public static readonly Model QWEN2_1_5B_INSTRUCT = new(
        "/data/local/tmp/Qwen2.5-1.5B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Qwen2.5-1.5B-Instruct/resolve/main/Qwen2.5-1.5B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "",
        false,
        Backend.Cpu,
        false,
        0.95f,
        40,
        1.0f
    );
    public static readonly Model QWEN2_5_3B_INSTRUCT = new(
        "/data/local/tmp/Qwen2.5-3B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/Qwen2.5-3B-Instruct/resolve/main/Qwen2.5-3B-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "",
        false,
        Backend.Cpu,
        false,
        0.95f,
        40,
        1.0f
    );
    public static readonly Model SMOLLM_135M_INSTRUCT = new(
        "/data/local/tmp/SmolLM-135M-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/SmolLM-135M-Instruct/resolve/main/SmolLM-135M-Instruct_multi-prefill-seq_q8_ekv1280.task",
        "",
        false,
        Backend.Cpu,
        false,
        0.95f,
        40,
        1.0f
    );
    public static readonly Model TINYLLAMA_1_1B_CHAT_V1_0 = new(
        "/data/local/tmp/TinyLlama-1.1B-Chat-v1.0_multi-prefill-seq_q8_ekv1280.task",
        "https://huggingface.co/litert-community/TinyLlama-1.1B-Chat-v1.0/resolve/main/TinyLlama-1.1B-Chat-v1.0_multi-prefill-seq_q8_ekv1280.task",
        "",
        false,
        Backend.Cpu,
        false,
        0.95f,
        40,
        1.0f
    );

    public string Path { get; }
    public string Url { get; }
    public string LicenseUrl { get; }
    public bool NeedsAuth { get; }
    public Backend PreferredBackend { get; }
    public bool Thinking { get; }
    public float Temperature { get; }
    public int TopK { get; }
    public float TopP { get; }

    Model(string path, string url, string licenseUrl, bool needsAuth, Backend preferredBackend, bool thinking, float temperature, int topK, float topP)
    {
        Path = path;
        Url = url;
        LicenseUrl = licenseUrl;
        NeedsAuth = needsAuth;
        PreferredBackend = preferredBackend;
        Thinking = thinking;
        Temperature = temperature;
        TopK = topK;
        TopP = topP;
    }
}
