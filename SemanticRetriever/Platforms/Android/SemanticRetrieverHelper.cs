using Android.Content;
using Android.OS;
using Android.Util;
using Java.Lang;
using MediaPipe.Tasks.Core;
using MediaPipe.Tasks.Retrieval.Components;
using MediaPipe.Tasks.Retrieval.SemanticRetriever;
using MediaPipe.Tasks.Retrieval.UniversalEmbedder;
using File = Java.IO.File;
using FileNotFoundException = Java.IO.FileNotFoundException;
using Uri = Android.Net.Uri;

namespace SemanticRetriever;

//
// Owns the embedding engine and the currently selected vector store.
//
// The [UniversalEmbedder] wraps a ~470 MB LiteRT-LM model ([MODEL_URL]), so it is created exactly
// once and kept alive for the lifetime of the helper. Switching vector stores only swaps the cheap
// [SemanticRetriever] / [VectorStore] pair on top of that same embedder — tearing the engine down
// and standing a second one up would either fail to mmap the model file or leave callers holding a
// closed engine.
//
// This class is not thread safe: callers must serialize access (see the single-thread dispatcher
// in [MainActivity]).
//

public class SemanticRetrieverHelper(Context context)
{
    private UniversalEmbedder universalEmbedder = null;
    private MediaPipe.Tasks.Retrieval.SemanticRetriever.SemanticRetriever semanticRetriever = null;
    private bool currentlyOnGpu = false;

    // True once a vector store has been opened and the helper can accept work. */
    public bool IsReady => semanticRetriever != null;

    //
    // Loads the embedding model. Safe to call repeatedly; the engine is only built the first time.
    //
    public void PrepareEmbedder(bool useGpu = true)
    {
        if (universalEmbedder != null && currentlyOnGpu == useGpu) return;

        // The accelerator is baked into the engine, so changing it means rebuilding it.
        if (universalEmbedder != null)
        {
            semanticRetriever?.Close();
            semanticRetriever = null;
            universalEmbedder?.Close();
            universalEmbedder = null;
        }

        // Copy the model from APK assets into filesDir on first launch so we can open a standalone
        // ParcelFileDescriptor for BaseOptions.setModelAssetFileDescriptor.
        var modelFile = new File(context.FilesDir, ModelName);
        if (!modelFile.Exists())
        {
            Log.Info(Tag, $"Copying {ModelName} from assets to filesDir…");
            try
            {
                using (var input = context.Assets.Open(ModelName))
                {
                    using (var output = new System.IO.FileStream(modelFile.AbsolutePath, System.IO.FileMode.Create, System.IO.FileAccess.Write))
                    {
                        input.CopyTo(output);
                    }
                }
            }
            catch (FileNotFoundException e)
            {
                throw new IllegalStateException(
                    $"Model {ModelName} not found in assets. Download it from {ModelUrl} and " +
                        $"place it in app/src/main/assets/{ModelName}.",
                    e
                );
            }
            Log.Info(Tag, $"Model copied ({modelFile.Length()} bytes).");
        }

        var del = useGpu ? Delegates.Gpu: Delegates.Cpu;
        var startedAt = JavaSystem.CurrentTimeMillis;        
        using (var pfd = ParcelFileDescriptor.Open(modelFile, ParcelFileMode.ReadOnly))
        {
            var baseOptions = BaseOptions.InvokeBuilder()
                .SetModelAssetFileDescriptor((Integer)pfd.Fd)
                .SetDelegate(del)
                .Build();

            var embedderOptions = UniversalEmbedderOptions.InvokeBuilder()
                .SetBaseOptions(baseOptions)
                .SetCacheDir(context.CacheDir.AbsolutePath)
                .Build();

            universalEmbedder = UniversalEmbedder.CreateFromOptions(context, embedderOptions);
        };
        currentlyOnGpu = useGpu;
        var elapsed = JavaSystem.CurrentTimeMillis - startedAt;
        Log.Info(Tag, $"Embedding engine ready on {del} in {elapsed}ms");
    }

    //
    // Closes the previous store (if any) and opens [useAppSearch] ? AppSearch : SQLite, reusing the
    // already loaded embedding engine.
    //
    public void OpenStore(bool useAppSearch)
    {
        ArgumentNullException.ThrowIfNull(universalEmbedder,
            "prepareEmbedder() must be called before openStore()");

        // SemanticRetriever.close() closes the vector store it was built with, and leaves the
        // embedder untouched — which is exactly what we want here.
        semanticRetriever?.Close();
        semanticRetriever = null;

        var vectorStore = (IVectorStore) (useAppSearch ?
            new AppSearchVectorStore(context, DbName) :
            new SqliteVectorStore(context, DbName, 768));

        var components = new SemanticRetrieverComponents()
            .SetVectorStore(vectorStore)
            .AddProvider(universalEmbedder.Provider);

        semanticRetriever = MediaPipe.Tasks.Retrieval.SemanticRetriever.SemanticRetriever.CreateFromComponents(context, components);
        Log.Info(Tag, $"Opened {(useAppSearch ? "AppSearch" : "SQLite")} vector store.");
    }

    //
    // Drops [ids] from the open store. Used to reset state when switching backends, which is safer
    // than deleting the database files out from under a live store.
    //
    public void Clear(List<string> ids)
    {
        semanticRetriever?.Delete(ids);
    }

    public void EmbedImage(string id, Uri uri)
    {
        ArgumentNullException.ThrowIfNull(semanticRetriever, "No vector store is open");
        semanticRetriever.InsertImage(id, uri);
    }

    public IList<RetrievalResult> SearchImages(string query, int limit = 5)
    {
        return semanticRetriever?.Retrieve(query, limit) ?? new List<RetrievalResult>();
    }

    public void Close()
    {
        semanticRetriever?.Close();
        semanticRetriever = null;
        universalEmbedder?.Close();
        universalEmbedder = null;
    }

    const string Tag = "SemanticRetriever";
    const string ModelName = "embeddinggemma-2-text-vision-440m.litertlm";
    const string ModelUrl =
        "https://huggingface.co/litert-community/embeddinggemma-2-text-vision-440m-litert-lm/" +
            "resolve/main/embeddinggemma-2-text-vision-440m.litertlm";
    const string DbName = "semantic_db";
}
