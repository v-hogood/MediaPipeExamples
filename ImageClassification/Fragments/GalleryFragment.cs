using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Provider;
using Android.Util;
using Android.Views;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using AndroidX.AppCompat.Widget;
using AndroidX.Core.Widget;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.FloatingActionButton;
using Java.Lang;
using Java.Util.Concurrent;
using MediaPipe.Tasks.Vision.Core;
using Fragment = AndroidX.Fragment.App.Fragment;
using Object = Java.Lang.Object;
using Uri = Android.Net.Uri;

namespace ImageClassification;

[Android.App.Activity(Name = "com.google.mediapipe.examples.imageclassification.fragments.GalleryFragment")]
class GalleryFragment : Fragment,
    ImageClassifierHelper.IClassifierListener,
    View.IOnClickListener,
    AdapterView.IOnItemSelectedListener,
    IActivityResultCallback,
    MediaPlayer.IOnPreparedListener
{
    enum MediaType
    {
        Image, Video, Unknown
    }

    private MainViewModel viewModel = new();
    private ImageClassifierHelper imageClassifierHelper;
    private ClassificationResultsAdapter classificationResultsAdapter = new();

    // Blocking ML operations are performed using this executor
    private IScheduledExecutorService backgroundExecutor;

    private NestedScrollView bottomSheet;

    private ActivityResultLauncher getContent;

    public void OnActivityResult(Object uri)
    {
        var mediaUri = uri as Uri;
        if (mediaUri != null)
        {
            // Handle the returned Uri
            var mediaType = LoadMediaType(mediaUri);
            switch (mediaType)
            {
                case MediaType.Image: RunClassificationOnImage(mediaUri); break;
                case MediaType.Video: RunClassificationOnVideo(mediaUri); break;
                case MediaType.Unknown:
                {
                    UpdateDisplayView(mediaType);
                    Toast.MakeText(
                        RequireContext(),
                        "Unsupported data type.",
                        ToastLength.Short
                    ).Show();
                    break;
                }
            }
        }
    }

    override public View OnCreateView(
        LayoutInflater inflater,
        ViewGroup container,
        Bundle savedInstanceState)
    {
        return inflater.Inflate(Resource.Layout.fragment_gallery, container, false);
    }

    override public void OnViewCreated(View view, Bundle savedInstanceState)
    {
        base.OnViewCreated(view, savedInstanceState);

        bottomSheet =
            RequireActivity().FindViewById<NestedScrollView>(Resource.Id.bottom_sheet_layouts);

        classificationResultsAdapter
            .UpdateAdapterSize(viewModel.MaxResults);

        getContent =
            RegisterForActivityResult(new ActivityResultContracts.OpenDocument(), this);

        var fabGetContent =
            RequireActivity().FindViewById<FloatingActionButton>(Resource.Id.fabGetContent);
        fabGetContent.SetOnClickListener(this);
        var recyclerviewResults =
            RequireActivity().FindViewById<RecyclerView>(Resource.Id.recyclerview_result);
        recyclerviewResults.SetLayoutManager(new LinearLayoutManager(RequireContext()));
        recyclerviewResults.SetAdapter(classificationResultsAdapter);

        InitBottomSheetControls();
    }

    private void InitBottomSheetControls()
    {
        UpdateControlsUi();
        // When clicked, lower classification score threshold floor
        bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.threshold_minus).SetOnClickListener(this);

        // When clicked, raise classification score threshold floor
        bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.threshold_plus).SetOnClickListener(this);

        // When clicked, reduce the number of objects that can be classified
        // at a time
        bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.max_results_minus).SetOnClickListener(this);

        // When clicked, increase the number of objects that can be
        // classified at a time
        bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.max_results_plus).SetOnClickListener(this);

        // When clicked, change the underlying hardware used for inference. Current options are CPU
        // GPU, and NNAPI
        var spinnerDelegate =
            bottomSheet.FindViewById<AppCompatSpinner>(Resource.Id.spinner_delegate);
        spinnerDelegate.SetSelection(viewModel.Delegate, false);
        spinnerDelegate.OnItemSelectedListener = this;

        // When clicked, change the underlying model used for image
        // classification
        var spinnerModel =
            bottomSheet.FindViewById<AppCompatSpinner>(Resource.Id.spinner_model);
        spinnerModel.SetSelection(viewModel.Model, false);
        spinnerModel.OnItemSelectedListener = this;
    }

    public void OnClick(View v)
    {
        if (v.Id == Resource.Id.threshold_minus)
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
        else if (v.Id == Resource.Id.max_results_minus)
        {
            if (viewModel.MaxResults > 1)
            {
                viewModel.MaxResults--;
                UpdateControlsUi();
                classificationResultsAdapter.UpdateAdapterSize(viewModel.MaxResults);
            }
        }
        else if (v.Id == Resource.Id.max_results_plus)
        {
            if (imageClassifierHelper.MaxResults < 3)
            {
                viewModel.MaxResults++;
                UpdateControlsUi();
                classificationResultsAdapter.UpdateAdapterSize(viewModel.MaxResults);
            }
        }
        else if (v.Id == Resource.Id.fabGetContent)
        {
            getContent.Launch(new string[] { "image/*", "video/*" });
            UpdateDisplayView(MediaType.Unknown);
        }
    }

    public void OnItemSelected(AdapterView parent, View view, int position, long id)
    {
        if (parent.Id == Resource.Id.spinner_delegate)
        {
            viewModel.Delegate = position;
            UpdateControlsUi();
        }
        else if (parent.Id == Resource.Id.spinner_model)
        {
            viewModel.Model = position;
            UpdateControlsUi();
        }
    }

    public void OnNothingSelected(AdapterView parent)
    {
        // no op
    }

    // Update the values displayed in the bottom sheet. Reset classifier.
    private void UpdateControlsUi()
    {
        var videoView = RequireActivity().FindViewById<VideoView>(Resource.Id.videoView);
        if (videoView.IsPlaying)
        {
            videoView.StopPlayback();
        }
        videoView.Visibility = ViewStates.Gone;
        var imageResult = RequireActivity().FindViewById<ImageView>(Resource.Id.imageResult);
        imageResult.Visibility = ViewStates.Gone;
        var maxResultsValue = bottomSheet.FindViewById<TextView>(Resource.Id.max_results_value);
        maxResultsValue.Text = viewModel.MaxResults.ToString();
        var thresholdValue = bottomSheet.FindViewById<TextView>(Resource.Id.threshold_value);
        thresholdValue.Text = viewModel.Threshold.ToString("0.00");
        var tvPlaceholder = RequireActivity().FindViewById<TextView>(Resource.Id.tvPlaceholder);
        tvPlaceholder.Visibility = ViewStates.Visible;
        classificationResultsAdapter.UpdateAdapterSize(viewModel.MaxResults);
        classificationResultsAdapter.UpdateResults(null);
        classificationResultsAdapter.NotifyDataSetChanged();
    }

    // Load and display the image.
    private void RunClassificationOnImage(Uri uri)
    {
        SetUiEnabled(false);
        backgroundExecutor = Executors.NewSingleThreadScheduledExecutor();
        UpdateDisplayView(MediaType.Image);
        Bitmap bitmap;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
        {
#pragma warning disable CA1416
            var source = ImageDecoder.CreateSource(
                RequireActivity().ContentResolver, uri
            );
            bitmap = ImageDecoder.DecodeBitmap(source);
#pragma warning restore
        }
        else
        {
#pragma warning disable CA1422
            bitmap = MediaStore.Images.Media.GetBitmap(
                RequireActivity().ContentResolver, uri
            );
#pragma warning restore
        }
        bitmap = bitmap.Copy(Bitmap.Config.Argb8888, true);
        if (bitmap != null)
        {
            var imageResult = RequireActivity().FindViewById<ImageView>(Resource.Id.imageResult);
            imageResult.SetImageBitmap(bitmap);

            // Run image classification on the input image
            backgroundExecutor.Execute(new Runnable(() =>
            {
                imageClassifierHelper = new ImageClassifierHelper(
                    context: RequireContext(),
                    runningMode: RunningMode.Image,
                    currentModel: viewModel.Model,
                    currentDelegate: viewModel.Delegate,
                    maxResults: viewModel.MaxResults,
                    threshold: viewModel.Threshold,
                    imageClassifierListener: this
                );
                var resultBundle = imageClassifierHelper.ClassifyImage(bitmap);
                if (resultBundle != null)
                {
                    RequireActivity().RunOnUiThread(() =>
                    {
                        classificationResultsAdapter.UpdateResults(
                            resultBundle?.Results.First()
                        );
                        classificationResultsAdapter.NotifyDataSetChanged();
                        SetUiEnabled(true);
                        var inferenceTimeVal = bottomSheet.FindViewById<TextView>(Resource.Id.inference_time_val);
                        inferenceTimeVal.Text =
                            resultBundle?.InferenceTime.ToString("0.00") + " ms";
                    });
                }
                else
                {
                    Log.Error(Tag, "Error running image classification.");
                }

                imageClassifierHelper.ClearImageClassifier();
            }));
        }
    }

    // Load and display the video.
    private void RunClassificationOnVideo(Uri uri)
    {
        SetUiEnabled(false);
        UpdateDisplayView(MediaType.Video);

        var videoView = RequireActivity().FindViewById<VideoView>(Resource.Id.videoView);
        videoView.SetVideoURI(uri);
        // mute the audio
        videoView.SetOnPreparedListener(this);
        videoView.RequestFocus();

        backgroundExecutor = Executors.NewSingleThreadScheduledExecutor();
        backgroundExecutor.Execute(new Runnable(() =>
        {
            RequireActivity().RunOnUiThread(() =>
            {
                videoView.Visibility = ViewStates.Gone;
                var progress = RequireActivity().FindViewById<ContentLoadingProgressBar>(Resource.Id.progress);
                progress.Visibility = ViewStates.Visible;
            });

            imageClassifierHelper = new ImageClassifierHelper(
                context: RequireContext(),
                runningMode: RunningMode.Video,
                currentModel: viewModel.Model,
                currentDelegate: viewModel.Delegate,
                maxResults: viewModel.MaxResults,
                threshold: viewModel.Threshold,
                imageClassifierListener: this
            );

            var resultBundle = imageClassifierHelper.ClassifyVideoFile(uri, VideoIntervalMs);
            if (resultBundle != null)
            {
                RequireActivity().RunOnUiThread(() =>
                    DisplayVideoResult((ImageClassifierHelper.ResultBundle)resultBundle));
            }
            else
            {
                Log.Error(Tag, "Error running image classification.");
            }

            imageClassifierHelper.ClearImageClassifier();
        }));
    }

    void MediaPlayer.IOnPreparedListener.OnPrepared(MediaPlayer mp)
    {
        mp.SetVolume(leftVolume: 0, rightVolume: 0);
    }

    // Setup and display the video.
    private void DisplayVideoResult(ImageClassifierHelper.ResultBundle result)
    {
        var videoView = RequireActivity().FindViewById<VideoView>(Resource.Id.videoView);
        videoView.Visibility = ViewStates.Visible;
        var progress = RequireActivity().FindViewById<ContentLoadingProgressBar>(Resource.Id.progress);
        progress.Visibility = ViewStates.Gone;

        videoView.Start();
        var videoStartTimeMs = SystemClock.UptimeMillis();

        backgroundExecutor.ScheduleAtFixedRate(new Runnable(() =>
        {
            RequireActivity().RunOnUiThread(() =>
            {
                var videoElapsedTimeMs =
                    SystemClock.UptimeMillis() - videoStartTimeMs;
                var resultIndex = (int)
                    (videoElapsedTimeMs / VideoIntervalMs);

                if (resultIndex >= result.Results.Count || videoView.Visibility == ViewStates.Gone)
                {
                    SetUiEnabled(true);
                    backgroundExecutor.Shutdown();
                }
                else
                {
                    classificationResultsAdapter.UpdateResults(result.Results[resultIndex]);
                    classificationResultsAdapter.NotifyDataSetChanged();
                    SetUiEnabled(false);

                    var inferenceTimeVal = bottomSheet.FindViewById<TextView>(Resource.Id.inference_time_val);
                    inferenceTimeVal.Text =
                        result.InferenceTime.ToString("0.00") + " ms";
                }
            });
        }), 0, VideoIntervalMs, TimeUnit.Milliseconds);
    }

    private void UpdateDisplayView(MediaType mediaType)
    {
        var imageResult = RequireActivity().FindViewById<ImageView>(Resource.Id.imageResult);
        imageResult.Visibility =
            mediaType == MediaType.Image ? ViewStates.Visible : ViewStates.Gone;
        var videoView = RequireActivity().FindViewById<VideoView>(Resource.Id.videoView);
        videoView.Visibility =
            mediaType == MediaType.Video ? ViewStates.Visible : ViewStates.Gone;
        var tvPlaceholder = RequireActivity().FindViewById<TextView>(Resource.Id.tvPlaceholder);
        tvPlaceholder.Visibility =
            mediaType == MediaType.Unknown ? ViewStates.Visible : ViewStates.Gone;
    }

    // Check the type of media that user selected.
    private MediaType LoadMediaType(Uri uri)
    {
        var mimeType = RequireContext().ContentResolver?.GetType(uri);
        if (mimeType != null)
        {
            if (mimeType.StartsWith("image")) return MediaType.Image;
            if (mimeType.StartsWith("video")) return MediaType.Video;
        }

        return MediaType.Unknown;
    }

    private void SetUiEnabled(bool enabled)
    {
        var fabGetContent =
            RequireActivity().FindViewById<FloatingActionButton>(Resource.Id.fabGetContent);
        fabGetContent.Enabled = enabled;
        var spinnerModel =
            bottomSheet.FindViewById<AppCompatSpinner>(Resource.Id.spinner_model);
        spinnerModel.Enabled = enabled;
        var thresholdMinus = bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.threshold_minus);
        thresholdMinus.Enabled = enabled;
        var thresholdPlus = bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.threshold_plus);
        thresholdPlus.Enabled = enabled;
        var maxResultsMinus = bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.max_results_minus);
        maxResultsMinus.Enabled = enabled;
        var maxResultsPlus = bottomSheet.FindViewById<AppCompatImageButton>(Resource.Id.max_results_plus);
        maxResultsPlus.Enabled = enabled;
        var spinnerDelegate =
            bottomSheet.FindViewById<AppCompatSpinner>(Resource.Id.spinner_delegate);
        spinnerDelegate.Enabled = enabled;
    }

    private void ClassifyingError()
    {
        RequireActivity().RunOnUiThread(() =>
        {
            var progress = RequireActivity().FindViewById<ContentLoadingProgressBar>(Resource.Id.progress);
            progress.Visibility = ViewStates.Gone;
            SetUiEnabled(true);
            UpdateDisplayView(MediaType.Unknown);
        });
    }

    public void OnError(string error, int errorCode)
    {
        ClassifyingError();
        RequireActivity().RunOnUiThread(() =>
        {
            Toast.MakeText(RequireContext(), error, ToastLength.Short).Show();
            if (errorCode == ImageClassifierHelper.GpuError)
            {
                var spinnerDelegate =
                    bottomSheet.FindViewById<AppCompatSpinner>(Resource.Id.spinner_delegate);
                spinnerDelegate.SetSelection(
                    ImageClassifierHelper.DelegateCpu,
                    false
                );
            }
        });
    }

    public void OnResults(ImageClassifierHelper.ResultBundle resultBundle)
    {
        // no-op
    }

    new private const string Tag = "GalleryFragment";

    // Value used to get frames at specific intervals for inference (e.g. every 300ms)
    private const long VideoIntervalMs = 300;
}
