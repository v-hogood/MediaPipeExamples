using Android.Content;
using Android.OS;
using Android.Util;
using Java.Lang;
using MediaPipe.Tasks.Decision;
using static MediaPipe.Tasks.Decision.DecisionMaker;
using File = Java.IO.File;

namespace DecisionMaker;

//
// Wraps [DecisionMaker] setup (model + delegate) and the three question types:
// Boolean, Choice and Score. All methods are blocking; call them off the main thread.
//
public class DecisionMakerHelper
{
    private Context context;
    public DecisionMakerHelper(Context context)
    {
        this.context = context;
    }
    
    public int CurrentDelegate = DelegateCpu;
    public int CurrentModel = ModelEmbeddingGemma;

    private MediaPipe.Tasks.Decision.DecisionMaker decisionMaker = null;

    // Creates the [DecisionMaker]. Throws if the model is missing or the delegate fails.
    public void SetupDecisionMaker()
    {
        ClearDecisionMaker();
        var options = new DecisionMakerOptions.Builder()
            .SetModelPath(ResolveModelPath(ModelFiles[CurrentModel]))
            .SetMaxNumTokens(MaxNumTokens)
            .SetDelegate(
                CurrentDelegate == DelegateGpu ?
                    DecisionMakerDelegate.Gpu : DecisionMakerDelegate.Cpu
            )
            .Build();
        decisionMaker = CreateFromOptions(context, options);
    }

    public void ClearDecisionMaker()
    {
        decisionMaker?.Close();
        decisionMaker = null;
    }

    public ResultBundle<BooleanResult> EvaluateBoolean(
        string text,
        string condition,
        float threshold
    ) => Timed(dm => dm.Evaluate(text, new BooleanQuestion(condition, threshold, 0, false)));

    public ResultBundle<ChoiceResult> EvaluateChoice(
        string text,
        Dictionary<string, string> options,
        string instructions
    ) => Timed(dm => dm.Evaluate(text, ChoiceQuestion.WithDescriptions(options, 0, instructions,
            ScoringMode.LengthNormalizedLogProb, false)));

    public ResultBundle<ScoreResult> EvaluateScore(
        string text,
        List<string> rubric,
        string instructions
    ) => Timed(dm => dm.Evaluate(text, new ScoreQuestion(rubric, 0, instructions)));

    private ResultBundle<TResult> Timed<TResult>(Func<MediaPipe.Tasks.Decision.DecisionMaker, TResult> block)
    {
        var dm = decisionMaker ?? throw new IllegalStateException("DecisionMaker is not initialized.");
        var start = SystemClock.UptimeMillis();
        var result = block(dm);
        return new ResultBundle<TResult>(result, SystemClock.UptimeMillis() - start);
    }

    //
    // Returns a readable path for [fileName]: a copy pushed to the device (see README), or the
    // APK asset downloaded by download_models.gradle (copied to internal storage, and copied
    // again after each app update so a newer bundled model is never shadowed by an old copy).
    //
    private string ResolveModelPath(string fileName)
    {
        var pushed = new List<File>
        {
            new File("/data/local/tmp/decision_models", fileName),
            new File(context.GetExternalFilesDir(null), fileName)
        }.FirstOrDefault(f => f != null && f.CanRead() && f.Length() > 0L);
        if (pushed != null) return pushed.AbsolutePath;

        if (context.Assets.List("")?.Contains(fileName) != true) {
            throw new ModelNotFoundException($"{fileName} not found. See the README to download the model.");
        }
        if (context.Assets.List("")?.Contains(fileName) != true) {
            throw new ModelNotFoundException($"{fileName} not found. See the README to download the model.");
        }
        var copy = new File(context.FilesDir, fileName);
        var appUpdatedAt = context.PackageManager.GetPackageInfo(context.PackageName, 0).LastUpdateTime;
        if (!copy.Exists() || copy.LastModified() < appUpdatedAt) {
            Log.Info(Tag, $"Copying {fileName} from assets to {copy.AbsolutePath}");
            var tmp = new File(context.FilesDir, fileName + ".tmp");
            using (var input = context.Assets.Open(fileName))
            {
                using (var output = new System.IO.FileStream(tmp.AbsolutePath, System.IO.FileMode.Create, System.IO.FileAccess.Write))
                {
                    input.CopyTo(output);
                }
            }
            if (!tmp.RenameTo(copy)) throw new IllegalStateException($"Could not copy {fileName}");
        }
        return copy.AbsolutePath;
    }

    // The model file is neither pushed to the device nor bundled in the APK.
    public class ModelNotFoundException(string message) : IllegalStateException(message);

    public record ResultBundle<T>(T result, long inferenceTime);

    public const int DelegateCpu = 0;
    public const int DelegateGpu = 1;

    const int ModelEmbeddingGemma = 0;
    const int ModelLaya = 1;
    const int ModelGliner = 2;

    // Model files, indexed by the MODEL_* constants (see download_models.gradle).
    private string[] ModelFiles =
    {
        "embeddinggemma-2-text-270m.litertlm",
        "laya_s256.task",
        "gliner_s256.task"
    };

    private const int MaxNumTokens = 4096;
    private const string Tag = "DecisionMakerHelper";
}
