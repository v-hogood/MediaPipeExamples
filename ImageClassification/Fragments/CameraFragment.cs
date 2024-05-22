using Android.Util;
using Android.Views;
using AndroidX.AppCompat.Widget;
using AndroidX.Camera.Core;
using AndroidX.Camera.Core.ResolutionSelector;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.Content;
using AndroidX.Navigation;
using AndroidX.RecyclerView.Widget;
using Java.Lang;
using Java.Util.Concurrent;
using MediaPipe.Tasks.Vision.Core;
using Exception = Java.Lang.Exception;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace ImageClassification;

[Android.App.Activity(Name = "com.google.mediapipe.examples.imageclassification.fragments.CameraFragment")]
public class CameraFragment : Fragment,
    ImageClassifierHelper.IClassifierListener,
    View.IOnClickListener,
    AdapterView.IOnItemSelectedListener
{
    private new const string Tag = "Image Classifier";

    private MainViewModel viewModel = new();
    private ImageClassifierHelper imageClassifierHelper;
    private ClassificationResultsAdapter classificationResultsAdapter = new();

    private PreviewView viewFinder = null;
    private Preview preview = null;
    private ImageAnalysis imageAnalyzer = null;
    private ICamera camera = null;
    private ProcessCameraProvider cameraProvider = null;

    // Blocking operations are performed using this executor
    private IExecutorService backgroundExecutor;

    public override void OnResume()
    {
        base.OnResume();

        if (!PermissionsFragment.HasPermissions(RequireContext()))
        {
            Navigation.FindNavController(
                RequireActivity(), Resource.Id.fragment_container
            ).Navigate(Resource.Id.action_camera_to_permissions);

            backgroundExecutor.Execute(new Runnable(() =>
            {
                if (imageClassifierHelper.IsClosed())
                {
                    imageClassifierHelper.SetupImageClassifier();
                }
            }));
        }
    }

    override public void OnPause()
    {
        // save ImageClassifier settings
        viewModel.Model = imageClassifierHelper.CurrentModel;
        viewModel.Delegate = imageClassifierHelper.CurrentDelegate;
        viewModel.Threshold = imageClassifierHelper.Threshold;
        viewModel.MaxResults = imageClassifierHelper.MaxResults;
        base.OnPause();

        // Close the image classifier and release resources
        backgroundExecutor.Execute(new Runnable(() => imageClassifierHelper.ClearImageClassifier()));
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

    public override View OnCreateView(
        LayoutInflater inflater,
        ViewGroup container,
        Bundle savedInstanceState)
    {
        return inflater.Inflate(Resource.Layout.fragment_camera, container, false);
    }

    public override void OnViewCreated(View view, Bundle savedInstanceState)
    {
        base.OnViewCreated(view, savedInstanceState);

        viewFinder =
            RequireActivity().FindViewById<PreviewView>(Resource.Id.view_finder);

        classificationResultsAdapter
            .UpdateAdapterSize(viewModel.MaxResults);

        var recyclerViewResults =
            RequireActivity().FindViewById<RecyclerView>(Resource.Id.recyclerview_results);
        recyclerViewResults.SetLayoutManager(new LinearLayoutManager(RequireContext()));
        recyclerViewResults.SetAdapter(classificationResultsAdapter);

        backgroundExecutor = Executors.NewSingleThreadExecutor();
        backgroundExecutor.Execute(new Runnable(() =>
        {
            imageClassifierHelper =
                new ImageClassifierHelper(
                    context: RequireContext(),
                    runningMode: RunningMode.LiveStream,
                    threshold: viewModel.Threshold,
                    currentDelegate: viewModel.Delegate,
                    currentModel: viewModel.Model,
                    maxResults: viewModel.MaxResults,
                    imageClassifierListener: this);

            viewFinder.Post(() =>
            {
                // Set up the camera and its use cases
                SetUpCamera();
            });
        }));

        // Attach listeners to UI control widgets
        InitBottomSheetControls();
    }

    // Initialize CameraX, and prepare to bind the camera use cases
    private void SetUpCamera()
    {
        var cameraProviderFuture = ProcessCameraProvider.GetInstance(RequireContext());
        cameraProviderFuture.AddListener(new Runnable(() =>
        {
            // CameraProvider
            cameraProvider = cameraProviderFuture.Get() as ProcessCameraProvider;

            // Build and bind the camera use cases
            BindCameraUseCases();
        }), ContextCompat.GetMainExecutor(RequireContext()));
    }

    private void InitBottomSheetControls()
    {
        // Init bottom sheet settings
        RequireActivity().FindViewById<TextView>(Resource.Id.max_results_value).Text =
            viewModel.MaxResults.ToString();

        RequireActivity().FindViewById<TextView>(Resource.Id.threshold_value).Text =
            viewModel.Threshold.ToString("0.00");

        // When clicked, lower classification score threshold floor
        RequireActivity().FindViewById<AppCompatImageButton>(Resource.Id.threshold_minus).SetOnClickListener(this);
        // When clicked, raise classification score threshold floor
        RequireActivity().FindViewById<AppCompatImageButton>(Resource.Id.threshold_plus).SetOnClickListener(this);

        // When clicked, reduce the number of objects that can be classified at a time
        RequireActivity().FindViewById<AppCompatImageButton>(Resource.Id.max_results_minus).SetOnClickListener(this);
        // When clicked, increase the number of objects that can be classified at a time
        RequireActivity().FindViewById<AppCompatImageButton>(Resource.Id.max_results_plus).SetOnClickListener(this);

        // When clicked, change the underlying hardware used for inference. Current options are CPU
        // GPU, and NNAPI
        var spinnerDelegate =
            RequireActivity().FindViewById<AppCompatSpinner>(Resource.Id.spinner_delegate);
        spinnerDelegate.SetSelection(viewModel.Delegate, false);
        spinnerDelegate.OnItemSelectedListener = this;

        // When clicked, change the underlying model used for object classification
        var spinnerModel =
            RequireActivity().FindViewById<AppCompatSpinner>(Resource.Id.spinner_model);
        spinnerModel.SetSelection(viewModel.Model, false);
        spinnerModel.OnItemSelectedListener = this;
    }

    public void OnClick(View v)
    {
        if (v.Id == Resource.Id.threshold_minus)
        {
            if (imageClassifierHelper.Threshold >= 0.2)
            {
                imageClassifierHelper.Threshold -= 0.1f;
                UpdateControlsUi();
            }
        }
        else if (v.Id == Resource.Id.threshold_plus)
        {
            if (imageClassifierHelper.Threshold < 0.8)
            {
                imageClassifierHelper.Threshold += 0.1f;
                UpdateControlsUi();
            }
        }
        else if (v.Id == Resource.Id.max_results_minus)
        {
            if (imageClassifierHelper.MaxResults > 1)
            {
                imageClassifierHelper.MaxResults--;
                UpdateControlsUi();
                classificationResultsAdapter.UpdateAdapterSize(imageClassifierHelper.MaxResults);
            }
        }
        else if (v.Id == Resource.Id.max_results_plus)
        {
            if (imageClassifierHelper.MaxResults < 3)
            {
                imageClassifierHelper.MaxResults++;
                UpdateControlsUi();
                classificationResultsAdapter.UpdateAdapterSize(imageClassifierHelper.MaxResults);
            }
        }
    }

    public void OnItemSelected(AdapterView parent, View view, int position, long id)
    {
        if (parent.Id == Resource.Id.spinner_delegate)
        {
            imageClassifierHelper.CurrentDelegate = position;
            UpdateControlsUi();
        }
        else if (parent.Id == Resource.Id.spinner_model)
        {
            imageClassifierHelper.CurrentModel = position;
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
        RequireActivity().FindViewById<TextView>(Resource.Id.max_results_value).Text =
            imageClassifierHelper.MaxResults.ToString();

        RequireActivity().FindViewById<TextView>(Resource.Id.threshold_value).Text =
            imageClassifierHelper.Threshold.ToString("0.00");

        backgroundExecutor.Execute(new Runnable(() =>
        {
            imageClassifierHelper.ClearImageClassifier();
            imageClassifierHelper.SetupImageClassifier();
        }));
    }

    public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        imageAnalyzer.TargetRotation =
            (int) viewFinder.Display.Rotation;
    }

    // Declare and bind preview, capture and analysis use cases
    private void BindCameraUseCases()
    {
        // CameraProvider
        if (cameraProvider == null)
            throw new IllegalStateException("Camera initialization failed.");

        // CameraSelector - makes assumption that we're only using the back camera
        var cameraSelector =
            new CameraSelector.Builder().RequireLensFacing(CameraSelector.LensFacingBack).Build();

        // ResolutionSelector
        var resolutionSelector = new ResolutionSelector.Builder().
            SetAspectRatioStrategy(AspectRatioStrategy.Ratio43FallbackAutoStrategy)
            .Build();

        // Preview. Only using the 4:3 ratio because this is the closest to our models
        preview =
            new Preview.Builder()
                .SetResolutionSelector(resolutionSelector)
                .SetTargetRotation((int)viewFinder.Display.Rotation)
                .Build();

        // ImageAnalysis. Using RGBA 8888 to match how our models work
        imageAnalyzer =
            new ImageAnalysis.Builder()
                .SetResolutionSelector(resolutionSelector)
                .SetTargetRotation((int)viewFinder.Display.Rotation)
                .SetBackpressureStrategy(ImageAnalysis.StrategyKeepOnlyLatest)
                .SetOutputImageFormat(ImageAnalysis.OutputImageFormatRgba8888)
                .Build();

        // The analyzer can then be assigned to the instance
        imageAnalyzer.SetAnalyzer(
            backgroundExecutor,
            imageClassifierHelper);

        // Must unbind the use-cases before rebinding them
        cameraProvider.UnbindAll();

        try
        {
            // A variable number of use-cases can be passed here -
            // camera provides access to CameraControl & CameraInfo
            camera = cameraProvider.BindToLifecycle(
                this, cameraSelector, preview, imageAnalyzer
            );

            // Attach the viewfinder's surface provider to preview use case
            preview?.SetSurfaceProvider(viewFinder.SurfaceProvider);
        }
        catch (Exception exc)
        {
            Log.Error(Tag, "Use case binding failed", exc);
        }
    }

    public void OnError(string error, int errorCode)
    {
        RequireActivity().RunOnUiThread(() =>
        {
            Toast.MakeText(RequireContext(), error, ToastLength.Short).Show();
            classificationResultsAdapter.UpdateResults(null);
            classificationResultsAdapter.NotifyDataSetChanged();

            if (errorCode == ImageClassifierHelper.GpuError)
            {
                RequireActivity().FindViewById<AppCompatSpinner>(Resource.Id.spinner_delegate).SetSelection(
                    ImageClassifierHelper.DelegateGpu, false
                );
            }
        });
    }

    public void OnResults(ImageClassifierHelper.ResultBundle resultBundle)
    {
        RequireActivity().RunOnUiThread(() =>
        {
            // Show result on bottom sheet
            classificationResultsAdapter.UpdateResults(
                resultBundle.Results.First()
            );
            classificationResultsAdapter.NotifyDataSetChanged();
            RequireActivity().FindViewById<TextView>(Resource.Id.inference_time_val).Text =
                resultBundle.InferenceTime + " ms";
        });
    }
}
