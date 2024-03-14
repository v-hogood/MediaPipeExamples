using Android.Media;
using Android.Util;
using Android.Views;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using AndroidX.AppCompat.Widget;
using AndroidX.Core.Content;
using AndroidX.Core.Widget;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.ProgressIndicator;
using Java.Lang;
using Java.Util.Concurrent;
using MediaPipe.Tasks.Audio.Core;
using MediaPipe.Tasks.Components.Containers;
using Fragment = AndroidX.Fragment.App.Fragment;
using Object = Java.Lang.Object;
using Thread = Java.Lang.Thread;
using Uri = Android.Net.Uri;

namespace AudioClassification;

[Android.App.Activity(Name = "com.google.mediapipe.examples.audioclassifier.fragment.LibraryFragment")]
public class LibraryFragment : Fragment,
    AudioClassifierHelper.IClassifierListener,
    View.IOnClickListener,
    IActivityResultCallback
{
    private NestedScrollView bottomSheet;

    private MainViewModel viewModel = new();
    private ActivityResultLauncher getContent;
    public void OnActivityResult(Object uri)
    {
        // Handle the returned Uri
        RunAudioClassification(uri as Uri);
    }

    private ProbabilitiesAdapter probabilitiesAdapter;
    private AudioClassifierHelper audioClassifierHelper = null;
    private ScheduledThreadPoolExecutor progressExecutor = null;
    private ScheduledThreadPoolExecutor backgroundExecutor = null;

    private MediaPlayer mediaPlayer = null;

    override public View OnCreateView(
        LayoutInflater inflater,
        ViewGroup container,
        Bundle savedInstanceState)
    {
        return inflater.Inflate(Resource.Layout.fragment_library, container, false);
    }

    override public void OnViewCreated(View view, Bundle savedInstanceState)
    {
        base.OnViewCreated(view, savedInstanceState);

        bottomSheet =
            Activity?.FindViewById<NestedScrollView>(Resource.Id.bottom_sheet_layouts);

        probabilitiesAdapter = new ProbabilitiesAdapter();
        getContent =
            RegisterForActivityResult(new ActivityResultContracts.GetContent(), this);

        var decoration = new DividerItemDecoration(
            RequireContext(), DividerItemDecoration.Vertical
        );
        decoration.Drawable =
            ContextCompat.GetDrawable(RequireContext(), Resource.Drawable.space_divider);
        var recyclerView = Activity?.FindViewById<RecyclerView>(Resource.Id.recycler_views);
        recyclerView.SetLayoutManager(new LinearLayoutManager(RequireContext()));
        recyclerView.SetAdapter(probabilitiesAdapter);
        recyclerView.AddItemDecoration(decoration);

        var fabGetContent =
            Activity?.FindViewById<FloatingActionButton>(Resource.Id.fabGetContent);
        fabGetContent.SetOnClickListener(this);

        InitBottomSheetControls();
    }

    override public void OnPause()
    {
        base.OnPause();
        ResetUi();
    }

    override public void OnDestroyView()
    {
#pragma warning disable CA1422
        base.OnDestroyView();
#pragma warning restore
        audioClassifierHelper?.StopAudioClassification();
    }

    private void ResetUi()
    {
        // stop audio when it in background
        mediaPlayer?.Stop();
        mediaPlayer = null;
        // stop all background tasks
        backgroundExecutor?.ShutdownNow();
        progressExecutor?.ShutdownNow();
        SetUiEnabled(true);
        Activity.FindViewById<LinearProgressIndicator>(Resource.Id.audioProgress).Visibility = ViewStates.Invisible;
        Activity.FindViewById<ProgressBar>(Resource.Id.classifierProgress).Visibility = ViewStates.Gone;
        probabilitiesAdapter.UpdateCategoryList(new List<Category>());
    }

    private void StartPickupAudio()
    {
        getContent.Launch("audio/*");
    }

    private void RunAudioClassification(Uri uri)
    {
        backgroundExecutor = new ScheduledThreadPoolExecutor(1);
        var audioProgress = Activity.FindViewById<LinearProgressIndicator>(Resource.Id.audioProgress);
        audioProgress.Visibility = ViewStates.Invisible;
        audioProgress.Progress = 0;
        Activity.FindViewById<ProgressBar>(Resource.Id.classifierProgress).Visibility = ViewStates.Gone;
        SetUiEnabled(false);

        // run on background to avoid block the ui
        backgroundExecutor?.Execute(new Runnable(() =>
        {
            audioClassifierHelper = new AudioClassifierHelper(
                context: RequireContext(),
                classificationThreshold: viewModel.Threshold,
                overlap: viewModel.OverlapPosition,
                numOfResults: viewModel.MaxResults,
                runningMode: RunningMode.AudioClips,
                listener: this
            );

            // prepare media player
            mediaPlayer = new MediaPlayer();
            mediaPlayer.SetAudioAttributes(
                new AudioAttributes.Builder()
                    .SetContentType(AudioContentType.Music)
                    .SetUsage(AudioUsageKind.Media).Build()
                );
            mediaPlayer.SetDataSource(RequireContext(), uri);
            mediaPlayer.Prepare();
            var audioDuration = mediaPlayer.Duration;

            var resultBundle =
                audioClassifierHelper?.ClassifyAudio(
                    uri.CreateAudioData(
                        RequireContext()
                    )
                );
            if (resultBundle != null)
            {
                // if the thread is interrupted, skip the next block code.
                if (Thread.CurrentThread().IsInterrupted)
                {
                    return;
                }
                progressExecutor = new ScheduledThreadPoolExecutor(1);
                var audioClassifierResults = resultBundle?.results;
                var maxProgressCount = audioClassifierResults.First().ClassificationResults().Count;
                var audioProgress = Activity.FindViewById<LinearProgressIndicator>(Resource.Id.audioProgress);
                audioProgress.Max = maxProgressCount;
                var amountToUpdate = audioDuration / maxProgressCount;
                var runnable = new Runnable(() =>
                {
                    Activity?.RunOnUiThread(() =>
                    {
                        if (amountToUpdate * audioProgress.Progress < audioDuration)
                        {
                            int progress =
                                audioProgress.Progress;
                            var categories =
                                audioClassifierResults.First()
                                    .ClassificationResults()
                                    ?[progress]?.Classifications()
                                    ?[0]
                                    ?.Categories() ?? new List<Category>();
                            probabilitiesAdapter.UpdateCategoryList(
                                categories
                            );

                            progress += 1;
                            // update the audio progress
                            audioProgress.Progress =
                                progress;

                            if (progress == maxProgressCount)
                            {
                                // stop the audio process.
                                progressExecutor?.ShutdownNow();
                                SetUiEnabled(true);
                            }
                        }
                    });
                });
                Activity?.RunOnUiThread(() =>
                {
                    // start audio
                    mediaPlayer?.Start();
                    progressExecutor?.ScheduleAtFixedRate(
                        runnable,
                        0,
                        (long)amountToUpdate,
                        TimeUnit.Milliseconds
                    );
                    Activity.FindViewById<ProgressBar>(Resource.Id.classifierProgress)
                        .Visibility = ViewStates.Gone;
                    audioProgress.Visibility = ViewStates.Visible;
                    bottomSheet.FindViewById<TextView>(Resource.Id.inference_time_val).Text =
                        resultBundle?.inferenceTime + " ms";
                });
            }
            else
            {
                Log.Error(Tag, "Error running audio classification.");
            }
        }));
    }

    private void InitBottomSheetControls()
    {
        // Allow the user to change the max number of results returned by the audio classifier.
        // Currently allows between 1 and 5 results, but can be edited here.
        bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.results_minus).SetOnClickListener(this);
        bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.results_plus).SetOnClickListener(this);

        // Allow the user to change the confidence threshold required for the classifier to return
        // a result. Increments in steps of 10%.
        bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.threshold_minus).SetOnClickListener(this);
        bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.threshold_plus).SetOnClickListener(this);

        bottomSheet.FindViewById<TextView>(Resource.Id.threshold_value)
            .Text = viewModel.Threshold.ToString("0.00");
        bottomSheet.FindViewById<TextView>(Resource.Id.results_value)
            .Text = viewModel.MaxResults.ToString();
        // hide overlap in audio clip mode
        bottomSheet.FindViewById<RelativeLayout>(Resource.Id.rlOverlap)
            .Visibility = ViewStates.Gone;
    }

    public void OnClick(View v)
    {
        if (v.Id == Resource.Id.results_minus)
        {
            if (viewModel.MaxResults > 1)
            {
                viewModel.MaxResults--;
                UpdateControlsUi();
            }
        }
        else if (v.Id == Resource.Id.results_plus)
        {
            if (viewModel.MaxResults < 5)
            {
                viewModel.MaxResults++;
                UpdateControlsUi();
            }
        }
        else if (v.Id == Resource.Id.threshold_minus)
        {
            if (viewModel.Threshold >= 0.2)
            {
                viewModel.Threshold -= 0.1f;
                UpdateControlsUi();
            }
        }
        else if (v.Id == Resource.Id.threshold_plus)
        {
            if (viewModel.Threshold < 0.8)
            {
                viewModel.Threshold += 0.1f;
                UpdateControlsUi();
            }
        }
        else if (v.Id == Resource.Id.fabGetContent)
        {
            StartPickupAudio();
        }
    }

    // Update the values displayed in the bottom sheet. Reset classifier.
    private void UpdateControlsUi()
    {
        bottomSheet.FindViewById<TextView>(Resource.Id.results_value)
            .Text = viewModel.MaxResults.ToString();
        bottomSheet.FindViewById<TextView>(Resource.Id.threshold_value)
            .Text = viewModel.Threshold.ToString("0.00");
        Activity.FindViewById<LinearProgressIndicator>(Resource.Id.audioProgress).Visibility = ViewStates.Invisible;
        probabilitiesAdapter.UpdateCategoryList(new List<Category>());
    }

    private void SetUiEnabled(bool enabled)
    {
        Activity.FindViewById<FloatingActionButton>(Resource.Id.fabGetContent).Enabled = enabled;

        bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.results_minus).Enabled = enabled;
        bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.results_plus).Enabled = enabled;
        bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.threshold_minus).Enabled = enabled;
        bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.threshold_plus).Enabled = enabled;
    }

    public void OnError(string error)
    {
        Activity?.RunOnUiThread(() =>
        {
            ResetUi();
            Toast.MakeText(RequireContext(), error, ToastLength.Short).Show();
        });
    }

    public void OnResult(AudioClassifierHelper.ResultBundle resultBundle)
    {
        // no-op
    }

    new private const string Tag = "LibraryFragment";
}
