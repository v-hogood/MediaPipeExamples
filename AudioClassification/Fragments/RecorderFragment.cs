using Android.Views;
using AndroidX.AppCompat.Widget;
using AndroidX.Core.Content;
using AndroidX.Navigation;
using AndroidX.RecyclerView.Widget;
using Java.Lang;
using Java.Util.Concurrent;
using MediaPipe.Tasks.Audio.Core;
using MediaPipe.Tasks.Components.Containers;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace AudioClassification;

[Android.App.Activity(Name = "com.google.mediapipe.examples.audioclassifier.fragment.RecorderFragment")]
public class RecorderFragment : Fragment,
    AudioClassifierHelper.IClassifierListener,
    AdapterView.IOnItemSelectedListener,
    View.IOnClickListener
{
    private AudioClassifierHelper audioClassifierHelper;
    private ProbabilitiesAdapter probabilitiesAdapter;
    private MainViewModel viewModel = new();

    private IExecutorService backgroundExecutor;

    public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
    {
        return inflater.Inflate(Resource.Layout.fragment_recorder, container, false);
    }

    public override void OnViewCreated(View view, Bundle savedInstanceState)
    {
        base.OnViewCreated(view, savedInstanceState);
        Activity.FindViewById<RelativeLayout>(Resource.Id.rlInferenceTime).Visibility =
            ViewStates.Gone;
        backgroundExecutor = Executors.NewSingleThreadExecutor();

        // init the result recyclerview
        probabilitiesAdapter = new();
        var decoration = new DividerItemDecoration(
            RequireContext(),
            DividerItemDecoration.Vertical
        );
        decoration.Drawable =
            ContextCompat.GetDrawable(RequireContext(), Resource.Drawable.space_divider);
        var recyclerView = Activity?.FindViewById<RecyclerView>(Resource.Id.recycler_view);
        recyclerView.SetLayoutManager(new LinearLayoutManager(RequireContext()));
        recyclerView.SetAdapter(probabilitiesAdapter);
        recyclerView.AddItemDecoration(decoration);

        backgroundExecutor.Execute(new Runnable(() =>
        {
            audioClassifierHelper =
                new AudioClassifierHelper(
                    context: RequireContext(),
                    classificationThreshold: viewModel.Threshold,
                    overlap: viewModel.OverlapPosition,
                    numOfResults: viewModel.MaxResults,
                    runningMode: RunningMode.AudioStream,
                    listener: this
                );
            Activity?.RunOnUiThread(() => InitBottomSheetControls());
        }));
    }

    public override void OnResume()
    {
        base.OnResume();
        // Make sure that all permissions are still present, since the
        // user could have removed them while the app was in paused state.
        if (!PermissionsFragment.HasPermissions(RequireContext()))
        {
            Navigation.FindNavController(
                RequireActivity(),
                Resource.Id.fragment_container
            )
                .Navigate(Resource.Id.action_audio_to_permissions);
        }
        backgroundExecutor.Execute(new Runnable(() =>
        {
            if (audioClassifierHelper.IsClosed())
            {
                audioClassifierHelper.InitClassifier();
            }
        }));
    }

    public override void OnPause()
    {
        base.OnPause();

        // save audio classifier settings
        viewModel.Threshold = audioClassifierHelper.ClassificationThreshold;
        viewModel.MaxResults = audioClassifierHelper.NumOfResults;
        viewModel.OverlapPosition = audioClassifierHelper.Overlap;

        backgroundExecutor.Execute(new Runnable(() =>
            audioClassifierHelper?.StopAudioClassification()
        ));
    }

    public override void OnDestroyView()
    {
        base.OnDestroyView();
        // Shut down our background executor
        backgroundExecutor.Shutdown();
        backgroundExecutor.AwaitTermination(
            Long.MaxValue, TimeUnit.Nanoseconds
        );
    }

    private void InitBottomSheetControls()
    {
        // Allow the user to change the amount of overlap used in classification. More overlap
        // can lead to more accurate resolves in classification.
        var spinnerOverlap = Activity.FindViewById<AppCompatSpinner>(Resource.Id.spinner_overlap);
        spinnerOverlap.OnItemSelectedListener = this;

        // Allow the user to change the max number of results returned by the audio classifier.
        // Currently allows between 1 and 5 results, but can be edited here.
        Activity.FindViewById<AppCompatImageButton>(Resource.Id.results_minus).SetOnClickListener(this);
        Activity.FindViewById<AppCompatImageButton>(Resource.Id.results_plus).SetOnClickListener(this);

        // Allow the user to change the confidence threshold required for the classifier to return
        // a result. Increments in steps of 10%.
        Activity.FindViewById<AppCompatImageButton>(Resource.Id.threshold_minus).SetOnClickListener(this);
        Activity.FindViewById<AppCompatImageButton>(Resource.Id.threshold_plus).SetOnClickListener(this);

        spinnerOverlap.SetSelection(viewModel.OverlapPosition, false);

        Activity.FindViewById<TextView>(Resource.Id.threshold_value).Text =
            viewModel.Threshold.ToString();
        Activity.FindViewById<TextView>(Resource.Id.results_value).Text =
            viewModel.MaxResults.ToString();
    }

    public void OnItemSelected(AdapterView parent, View view, int position, long id)
    {
        audioClassifierHelper.Overlap = position;
        UpdateControlsUI();
    }

    public void OnNothingSelected(AdapterView parent)
    {
        // no op
    }

    public void OnClick(View v)
    {
        if (v.Id == Resource.Id.results_minus)
        {
            if (audioClassifierHelper.NumOfResults > 1)
            {
                audioClassifierHelper.NumOfResults--;
                UpdateControlsUI();
            }
        }
        else if (v.Id == Resource.Id.results_plus)
        {
            if (audioClassifierHelper.NumOfResults < 5)
            {
                audioClassifierHelper.NumOfResults++;
                UpdateControlsUI();
            }
        }
        else if (v.Id == Resource.Id.threshold_minus)
        {
            if (audioClassifierHelper.ClassificationThreshold >= 0.2)
            {
                audioClassifierHelper.ClassificationThreshold -= 0.1f;
                UpdateControlsUI();
            }
        }
        else if (v.Id == Resource.Id.threshold_plus)
        {
            if (audioClassifierHelper.ClassificationThreshold <= 0.8)
            {
                audioClassifierHelper.ClassificationThreshold += 0.1f;
                UpdateControlsUI();
            }
        }
    }

    // Update the values displayed in the bottom sheet. Reset classifier.
    private void UpdateControlsUI()
    {
        Activity.FindViewById<TextView>(Resource.Id.results_value).Text =
            audioClassifierHelper.NumOfResults.ToString();
        Activity.FindViewById<TextView>(Resource.Id.threshold_value).Text =
            audioClassifierHelper.ClassificationThreshold.ToString("0.00");

        backgroundExecutor.Execute(new Runnable(() =>
        {
            audioClassifierHelper.StopAudioClassification();
            audioClassifierHelper.InitClassifier();
        }));
    }

    public void OnError(string error)
    {
        Activity?.RunOnUiThread(() =>
        {
            Toast.MakeText(RequireContext(), error, ToastLength.Short).Show();
            probabilitiesAdapter.UpdateCategoryList(new List<Category>());
        });
    }

    public void OnResult(AudioClassifierHelper.ResultBundle resultBundle)
    {
        Activity?.RunOnUiThread(() =>
        {
            // Show results on bottom sheet
            probabilitiesAdapter.UpdateCategoryList(
                resultBundle.results[0].ClassificationResults().First()
                    .Classifications()[0].Categories()
                );
        });
    }
}
