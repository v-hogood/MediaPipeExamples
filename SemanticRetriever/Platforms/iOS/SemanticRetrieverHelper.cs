using CoreAnimation;
using Foundation;
using MediaPipeTasksRetrieval;
using static MediaPipeTasksRetrieval.Constants;

namespace SemanticRetriever;

// Owns the embedding engine and the vector store backing the search index.
//
// The `UniversalEmbedder` wraps a ~470 MB LiteRT model, so it is created exactly once and kept
// alive for the lifetime of the helper. Reopening the store only swaps the cheap
// `SemanticRetriever` / `SqliteVectorStore` pair on top of that same embedder — tearing the engine
// down and standing a second one up would either fail to mmap the model file or leave callers
// holding a released engine.
//
// This class is not thread safe: callers must serialize access (see the serial queue in
// `ViewController`).
public class SemanticRetrieverHelper
{
    // Name of the EmbeddingGemma V2 model bundled with the app.
    const string modelName = "embeddinggemma-2-text-vision-440m";
    const string modelExtension = "litertlm";

    private const string databaseName = "semantic_db.sqlite";

    private MPPUniversalEmbedder universalEmbedder;
    private MPPSemanticRetriever semanticRetriever;
    private bool currentlyOnGPU = false;

    // True once a vector store has been opened and the helper can accept work.
    public bool IsReady => semanticRetriever != null;

    // Absolute path of the SQLite file the vector store writes to.
    string databasePath { get
    {
        var documents = NSFileManager.DefaultManager.GetUrls(directory: NSSearchPathDirectory.DocumentDirectory, domains: NSSearchPathDomain.User)[0];
        return documents.Append(pathComponent: SemanticRetrieverHelper.databaseName, true).Path;
    }}

    // Loads the embedding model. Safe to call repeatedly; the engine is only built the first time,
    // or rebuilt when the requested accelerator changed.
    public void PrepareEmbedder(bool useGPU)
    {
        if (universalEmbedder != null && currentlyOnGPU == useGPU) return;

        // The accelerator is baked into the engine, so changing it means rebuilding it. Drop the
        // retriever first: it holds on to the provider we are about to release.
        semanticRetriever = null;
        universalEmbedder = null;

        var modelPath = NSBundle.MainBundle.PathForResource(
            name: modelName, ofType: modelExtension);
        if (modelPath == null)
            throw HelperError.ModelNotFound;

        var options = new MPPUniversalEmbedderOptions();
        options.BaseOptions.ModelAssetPath = modelPath;
        // Delegates are per-modality: the text tower encodes queries, the vision tower encodes images.
        // Both are pushed onto the same accelerator here.
        var del = useGPU ? MPPDelegate.Gpu : MPPDelegate.Cpu;
        options.TextDelegate = del;
        options.VisionDelegate = del;
        options.CacheDir =
            NSFileManager.DefaultManager.GetUrls(directory: NSSearchPathDirectory.CachesDirectory, domains: NSSearchPathDomain.User)[0].Path;
        // Embeddings are unit length, so a dot product is the cosine similarity.
        options.L2Normalize = true;

        var startedAt = CAAnimation.CurrentMediaTime();
        universalEmbedder = new MPPUniversalEmbedder(options: options,
            out NSError error);
        if (error != null)
            throw new HelperError(error.LocalizedDescription);
        currentlyOnGPU = useGPU;
        Console.WriteLine(
            string.Format(
                "Embedding engine ready on {0} in {1}ms", useGPU ? "GPU" : "CPU",
                (CAAnimation.CurrentMediaTime() - startedAt) * 1000));
    }

    // Closes the previous store (if any) and opens a SQLite backed one, reusing the already loaded
    // embedding engine.
    public void OpenStore()
    {
        if (universalEmbedder == null)
            throw HelperError.EmbedderNotPrepared;
        var embedder = universalEmbedder;

        // Releasing the retriever also releases the vector store it was built with, and leaves the
        // embedder untouched — which is exactly what we want here.
        semanticRetriever = null;

        var vectorStore = new MPPSqliteVectorStore(
            databasePath: databasePath,
            embeddingDimension: MPPSemanticRetrieverDefaultEmbeddingDimension);
        var components = new MPPSemanticRetrieverComponents(
            vectorStore: vectorStore, chunker: null, providers: [embedder],
            out NSError error);
        if (error != null)
            throw new HelperError(error.LocalizedDescription);

        semanticRetriever = new MPPSemanticRetriever(components: components,
            out error);
        if (error != null)
            throw new HelperError(error.LocalizedDescription);
        Console.WriteLine("Opened SQLite vector store at " + databasePath);
    }

    // Empties the open store. Used to reset state on launch, which is safer than deleting the
    // database file out from under a live store.
    public void Clear()
    {
        semanticRetriever.DeleteAllRecordsWithError(out NSError error);
        if (error != null)
            throw new HelperError(error.LocalizedDescription);
    }

    public void EmbedImage(string id, string path)
    {
        if (semanticRetriever == null)
            throw HelperError.StoreNotOpen;
        semanticRetriever.InsertImageWithId(recordId: id, filePath: path,
            out NSError error);
        if (error != null)
            throw new HelperError(error.LocalizedDescription);
    }

    public MPPRetrievalResult[] SearchImages(string query, int limit = 5)
    {
        if (semanticRetriever == null)
            throw HelperError.StoreNotOpen;
        var result = semanticRetriever.RetrieveWithText(queryText: query, topK: limit,
            out NSError error);
        if (error != null)
            throw new HelperError(error.LocalizedDescription);
        return result;
    }

    public void Close()
    {
        semanticRetriever = null;
        universalEmbedder = null;
    }

    class HelperError : Exception
    {
        public HelperError(string message) : base(message) { }

        public static HelperError ModelNotFound =>
            new($"{modelName}.{modelExtension} is missing from the app bundle.");

        public static HelperError EmbedderNotPrepared =>
            new("prepareEmbedder(useGPU:) must be called before openStore().");

        public static HelperError StoreNotOpen =>
            new("No vector store is open.");
    }
}
