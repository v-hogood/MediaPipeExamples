using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.Button;
using Google.Android.Material.Chip;
using Google.Android.Material.ImageView;
using Google.Android.Material.ProgressIndicator;
using Google.Android.Material.SwitchMaterial;
using Google.Android.Material.TextField;
using Java.Lang;
using Java.Util.Concurrent;
using Kotlin.Jvm.Functions;
using MediaPipe.Tasks.Retrieval.SemanticRetriever;
using Xamarin.KotlinX.Coroutines;
using Exception = Java.Lang.Exception;
using Object = Java.Lang.Object;
using ProgressBar = Android.Widget.ProgressBar;
using Uri = Android.Net.Uri;
using View = Android.Views.View;

namespace SemanticRetriever;

[Activity(Name = "com.google.mediapipe.examples.semanticretriever.MainActivity", Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
public class MainActivity : AppCompatActivity,
    View.IOnClickListener,
    TextInputEditText.IOnEditorActionListener
{
    private SemanticRetrieverHelper helper = null;
    private TextView tvStatus;
    private TextView tvEmpty;
    private ProgressBar spinnerStatus;
    private LinearProgressIndicator progressIndex;
    private MaterialButton btnEmbedImages;
    private RecyclerView rvResults;
    private ResultsAdapter resultsAdapter;
    private TextInputEditText etQuery;
    private TextInputLayout tilQuery;
    private ChipGroup chipGroup;
    private MaterialButtonToggleGroup toggleStore;
    private SwitchMaterial switchGpu;

    // True while the model is loading, indexing or searching. */
    private bool isBusy = false;

    // True once the sample images have been embedded into the current vector store. */
    private bool isIndexed = false;

    //
    // Every call into the retriever runs here. A single thread guarantees that model loading,
    // store switching, indexing and searching can never overlap — the native engine is not
    // reentrant, and overlapping work was what produced "EmbeddingEngine is not initialized".
    //
    private IExecutorService retrieverExecutor = Executors.NewSingleThreadExecutor();
    private ICoroutineScope uiScope = CoroutineScopeKt.CoroutineScope(SupervisorKt.SupervisorJob(null).Plus(Dispatchers.Main.Immediate));

    // In-flight store switch, cancelled if the user toggles again before it lands. */
    private IJob storeJob = null;

    private bool useAppSearch = true;
    private bool useGpu = true;

    private List<string> sampleImages = new()
    {
        "red_apple.jpg",
        "yellow_banana.jpg",
        "cute_cat.jpg",
        "fast_car.jpg",
        "green_tree.jpg",
        "blue_sky.jpg",
        "coffee_mug.jpg",
        "open_book.jpg",
        "sunny_beach.jpg",
        "snowy_mountain.jpg"
    };

    private List<string> suggestions = new()
    {
        "a piece of fruit",
        "an animal",
        "something to drink",
        "a vacation spot",
        "a vehicle"
    };

    override protected void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        tvStatus = FindViewById<TextView>(Resource.Id.tvStatus);
        tvEmpty = FindViewById<TextView>(Resource.Id.tvEmpty);
        spinnerStatus = FindViewById<ProgressBar>(Resource.Id.spinnerStatus);
        progressIndex = FindViewById<LinearProgressIndicator>(Resource.Id.progressIndex);
        btnEmbedImages = FindViewById<MaterialButton>(Resource.Id.btnEmbedImages);
        rvResults = FindViewById<RecyclerView>(Resource.Id.rvResults);
        etQuery = FindViewById<TextInputEditText>(Resource.Id.etQuery);
        tilQuery = FindViewById<TextInputLayout>(Resource.Id.tilQuery);
        chipGroup = FindViewById<ChipGroup>(Resource.Id.chipSuggestions);

        resultsAdapter = new ResultsAdapter();
        rvResults.SetLayoutManager(new LinearLayoutManager(this));
        rvResults.SetAdapter(resultsAdapter);

        toggleStore = FindViewById<MaterialButtonToggleGroup>(Resource.Id.toggleStore);
        toggleStore.Check(Resource.Id.btnAppSearch);
        toggleStore.ButtonChecked += (sender, e) =>
        {
            // Only react to the newly selected button, not the deselected one.
            if (e.P2)
            {
                useAppSearch = e.P1 == Resource.Id.btnAppSearch;
                Reload();
            }
        };

        switchGpu = FindViewById<SwitchMaterial>(Resource.Id.switchGpu);
        switchGpu.CheckedChange += (sender, e) =>
        {
            useGpu = e.IsChecked;
            Reload();
        };

        btnEmbedImages.Click += (sender, e) => EmbedSampleImages();

        tilQuery.SetEndIconOnClickListener(this);
        etQuery.SetOnEditorActionListener(this);

        foreach (var suggestion in suggestions)
        {
            var chip = new Chip(this)
            {
                Text = suggestion,
                Clickable = true,
                Checkable = false
            };
            chip.Click += (sender, e) =>
            {
                etQuery.Text = suggestion;
                RunSearch();
            };
            chipGroup.AddView(chip);
        }

        Reload();
    }

    public void OnClick(View v)
    {
        RunSearch();
    }

    public bool OnEditorAction(TextView v, ImeAction actionId, Android.Views.KeyEvent e)
    {
        if (actionId == ImeAction.Search)
        {
            RunSearch();
            return true;
        }
        else
        {
            return false;
        }
    }

    private void RunSearch()
    {
        if (isBusy)
        {
            Toast.MakeText(
                this,
                "Busy — wait for the current step to finish",
                ToastLength.Short
            ).Show();
            return;
        }
        var query = etQuery.Text?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            Toast.MakeText(this, "Enter a search query first", ToastLength.Short).Show();
            return;
        }
        HideKeyboard();
        SearchImages(query);
    }

    private void HideKeyboard()
    {
        var imm = GetSystemService(InputMethodService) as InputMethodManager;
        imm.HideSoftInputFromWindow(etQuery.WindowToken, 0);
    }

    // Shows/hides the small spinner next to the status line and disables input while busy.
    private void SetBusy(bool busy, string status)
    {
        isBusy = busy;
        spinnerStatus.Visibility = busy ? ViewStates.Visible : ViewStates.Gone;
        btnEmbedImages.Enabled = !busy;
        switchGpu.Enabled = !busy;
        // Disabling the group alone does not propagate to its buttons.
        for (int i = 0; i < toggleStore.ChildCount; i++)
        {
            toggleStore.GetChildAt(i).Enabled = !busy;
        }
        tvStatus.Text = status;
        RefreshSearchControls();
    }

    //
    // Indexing is a one-shot action per vector store: once the samples are in, the index
    // button goes away and the search UI takes over.
    //
    private void SetIndexed(bool indexed)
    {
        isIndexed = indexed;
        btnEmbedImages.Visibility = indexed ? ViewStates.Gone : ViewStates.Visible;
        RefreshSearchControls();
        UpdateEmptyState();
    }

    // Searching only makes sense once something has been indexed and nothing else is running.
    private void RefreshSearchControls()
    {
        var enabled = isIndexed && !isBusy;
        tilQuery.Enabled = enabled;
        etQuery.Enabled = enabled;
        tilQuery.Alpha = enabled ? 1f : 0.5f;
        chipGroup.Alpha = enabled ? 1f : 0.5f;
        for (int i = 0; i < chipGroup.ChildCount; i++)
        {
            chipGroup.GetChildAt(i).Enabled = enabled;
        }
    }

    private void Reload()
    {
        // Switching stores is a full reset: no index, no query, no results.
        storeJob?.Cancel(null);
        SetIndexed(false);
        var accelerator = useGpu ? "GPU" : "CPU";
        var storeName = useAppSearch ? "AppSearch" : "SQLite";
        SetBusy(true, "Loading " + storeName + " on " + accelerator + "…");
        etQuery.Text = "";
        progressIndex.Visibility = ViewStates.Gone;
        progressIndex.Progress = 0;
        resultsAdapter.SetResults(new List<RetrievalResult>());
        UpdateEmptyState();

        storeJob = uiScope.Launch(async () =>
        {
            try
            {
                await RunRetrieverAsync(() =>
                {
                    var retriever = helper = helper ?? new SemanticRetrieverHelper(this);
                    // Loads the model on first run only; subsequent switches reuse the engine.
                    retriever.PrepareEmbedder(useGpu);
                    retriever.OpenStore(useAppSearch);
                    // Start from a known-empty store without deleting files under a live store.
                    retriever.Clear(sampleImages.Select(it => it.Split(".")[0]).ToList());
                });
                SetBusy(false, $"Ready · {storeName} on {accelerator} · store is empty");
                SetIndexed(false);
            }
            catch (Exception e)
            {
                Log.Error("MainActivity", "Error opening store", e);
                SetBusy(false, $"Initialization failed: {e.Message}");
            }
        });
    }

    private void EmbedSampleImages()
    {
        var retriever = helper;
        if (retriever == null || !retriever.IsReady || isBusy)
        {
            Toast.MakeText(this, "Still getting ready, please wait…", ToastLength.Short).Show();
            return;
        }
        SetBusy(true, "Indexing…");
        progressIndex.Visibility = ViewStates.Visible;
        progressIndex.Progress = 0;
        var started = JavaSystem.CurrentTimeMillis();
        uiScope.Launch(async () =>
        {
            try
            {
                for (int index = 0; index < sampleImages.Count; index++)
                {
                    var imageName = sampleImages[index];
                    var id = imageName.Split(".")[0];
                    var label = id.Replace('_', ' ');
                    tvStatus.Text = $"Embedding {index + 1}/{sampleImages.Count}: {label}";
                    progressIndex.SetProgressCompat(index * 100 / sampleImages.Count, true);
                    await RunRetrieverAsync(() =>
                    {
                        var assetUri = Uri.Parse($"file:///android_asset/images/{imageName}");
                        retriever.EmbedImage(id, assetUri);
                    });
                    Log.Debug("MainActivity", $"Embedded {id}");
                }
                var seconds = (JavaSystem.CurrentTimeMillis() - started) / 1000.0;
                var accelerator = useGpu ? "GPU" : "CPU";
                progressIndex.SetProgressCompat(100, true);
                // Let the bar finish animating, then tuck it away again.
                progressIndex.PostDelayed(() => progressIndex.Visibility = ViewStates.Gone, 600);
                SetBusy(
                    false,
                    $"Indexed {sampleImages.Count} images in {seconds:F1}s on {accelerator} · ready to search"
                );
                SetIndexed(true);
            }
            catch (Exception e)
            {
                Log.Error("MainActivity", "Error embedding", e);
                progressIndex.Visibility = ViewStates.Gone;
                SetBusy(false, $"Indexing failed: {e.Message}");
            }
        });
    }

    private Task RunRetrieverAsync(Action action)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        retrieverExecutor.Execute(new Runnable(() =>
        {
            action();
            completion.SetResult(true);
        }));
        return completion.Task;
    }

    private void SearchImages(string query)
    {
        var retriever = helper;
        if (retriever == null || !retriever.IsReady)
        {
            Toast.MakeText(this, "Still getting ready, please wait…", ToastLength.Short).Show();
            return;
        }
        SetBusy(true, $"Searching for {query}…");
        uiScope.Launch(async () =>
        {
            try
            {
                var started = JavaSystem.CurrentTimeMillis();
                IList<RetrievalResult> results = null;
                await RunRetrieverAsync(() =>
                {
                    results = retriever.SearchImages(query, 5);
                });
                var millis = JavaSystem.CurrentTimeMillis() - started;
                SetBusy(false, $"{results.Count} results for {query} in {millis}ms");
                resultsAdapter.SetResults(results);
                UpdateEmptyState();
            }
            catch (Exception e)
            {
                Log.Error("MainActivity", "Error searching", e);
                SetBusy(false, $"Search failed: {e.Message}");
            }
        });
    }

    private void UpdateEmptyState()
    {
        tvEmpty.Visibility = resultsAdapter.ItemCount == 0 ? ViewStates.Visible : ViewStates.Gone;
        tvEmpty.Text = GetString(isIndexed ? Resource.String.empty_no_results : Resource.String.empty_not_indexed);
    }

    override protected void OnDestroy()
    {
        base.OnDestroy();
        CoroutineScopeKt.Cancel(uiScope, null);
        var retriever = helper;
        helper = null;
        // Close off the UI thread: releasing the native engine can block.
        Executors.NewSingleThreadExecutor().Execute(new Runnable(() =>
        {
            retriever?.Close();
        }));
    }
}

class ResultsAdapter : RecyclerView.Adapter
{
    private List<RetrievalResult> results = new List<RetrievalResult>();

    // Assets never change at runtime, so a tiny in-memory cache is all we need. Decoding
    // directly from assets also avoids stale entries from an image loader's disk cache.
    private LruCache bitmapCache = new(16);

    public void SetResults(IList<RetrievalResult> newResults)
    {
        results.Clear();
        results.AddRange(newResults);
        NotifyDataSetChanged();
    }

    override public RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        var view = LayoutInflater.From(parent.Context)
            .Inflate(Resource.Layout.item_search_result, parent, false);
        return new ResultViewHolder(view);
    }

    override public void OnBindViewHolder(RecyclerView.ViewHolder vh, int position)
    {
        var holder = vh as ResultViewHolder;
        var result = results[position];
        var id = result.Id();
        holder.tvRank.Text = (position + 1).ToString();
        var text = id.Replace('_', ' ');
        holder.tvId.Text = char.ToUpper(text[0]) + text[1..];

        var score = result.Score();
        holder.tvScore.Text = string.Format("Similarity {0:F4}", score);
        // Scores are cosine similarities in [-1, 1]; map to a 0-100 bar.
        holder.pbScore.SetProgressCompat(System.Math.Clamp((int)((score + 1f) / 2f * 100), 0, 100), true);

        holder.ivResult.SetImageBitmap(LoadAsset(holder.ItemView.Context, $"images/{id}.jpg"));
    }

    private Bitmap LoadAsset(Context context, string path)
    {
        if (bitmapCache.Get(path) is Bitmap it)
            return it;

        try
        {
            var options = new BitmapFactory.Options { InSampleSize = 4 };
            using (var stream = context.Assets.Open(path))
            {
                var bitmap = BitmapFactory.DecodeStream(stream, null, options);
                bitmapCache.Put(path, bitmap);
                return bitmap;
            }
        }
        catch (Exception e)
        {
            Log.Error("ResultsAdapter", "Unable to load asset $path", e);
            return null;
        }
    }

    override public int ItemCount => results.Count;

    public class ResultViewHolder(View itemView) : RecyclerView.ViewHolder(itemView)
    {
        public ShapeableImageView ivResult = itemView.FindViewById<ShapeableImageView>(Resource.Id.ivResult);
        public TextView tvRank = itemView.FindViewById<TextView>(Resource.Id.tvRank);
        public TextView tvId = itemView.FindViewById<TextView>(Resource.Id.tvId);
        public TextView tvScore = itemView.FindViewById<TextView>(Resource.Id.tvScore);
        public LinearProgressIndicator pbScore = itemView.FindViewById<LinearProgressIndicator>(Resource.Id.pbScore);
    }
}

public static class BuildersKtx
{
    public class Function2 : Object, IFunction2
    { 
        Action action;
        public Function2(Action action) => this.action = action;
        public Object Invoke(Object p0, Object p1)
        {
            action();
            return null;
        }
    }

    public static IJob Launch(this ICoroutineScope scope, Action action) =>
        BuildersKt.Launch(scope, scope.CoroutineContext, CoroutineStart.Default, new Function2(action));
}  
