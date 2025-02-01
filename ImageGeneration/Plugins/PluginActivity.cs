using Android.Content;
using Android.Graphics;
using Android.Provider;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using AndroidX.Lifecycle;
using Kotlin.Coroutines;
using MediaPipe.Tasks.Vision.ImageGenerator;
using Xamarin.KotlinX.Coroutines.Flow;
using static AndroidX.Lifecycle.LifecycleOwnerKt;
using static AndroidX.Lifecycle.RepeatOnLifecycleKt;
using static ImageGeneration.BuildersKtx;
using static ImageGeneration.PluginViewModel;
using Object = Java.Lang.Object;
using Random = Java.Util.Random;
using Uri = Android.Net.Uri;

namespace ImageGeneration;

[Activity(Name = "com.google.mediapipe.examples.imagegeneration.plugins.PluginActivity", Label = "@string/app_name", Theme = "@style/AppTheme")]
public class PluginActivity : AppCompatActivity,
    View.IOnClickListener,
    RadioGroup.IOnCheckedChangeListener,
    AdapterView.IOnItemSelectedListener,
    IActivityResultCallback,
    IFlowCollector,
    IContinuation
{
    private const int DefaultDisplayIteration = 5;
    private const int DefaultIteration = 20;
    private const int DefaultSeed = 0;
    private int DefaultDisplayOptions = Resource.Id.radio_final; // FINAL

    private PluginViewModel viewModel = new();

    private ActivityResultLauncher openGalleryResultLauncher;
    private ActivityResultLauncher openCameraResultLauncher;

    ICoroutineContext IContinuation.Context => GetLifecycleScope(this).CoroutineContext;

    public void ResumeWith(Object result) { }

    void IActivityResultCallback.OnActivityResult(Object obj)
    {
        var result = obj as ActivityResult;
        if (result.ResultCode == (int)Result.Ok)
        {
            var bitmap = result.Data?.Extras?.Get("data") as Bitmap;
            var uri = result.Data?.Data as Uri;
            if (uri != null)
            {
                bitmap = ImageUtils.DecodeBitmapFromUri(this, uri);
            }
            if (bitmap != null)
            {
                viewModel.UpdateInputBitmap(CropBitmapToSquare(bitmap));
            }
        }
    }

    public PluginActivity()
    {
        openGalleryResultLauncher = RegisterForActivityResult(new ActivityResultContracts.StartActivityForResult(), this);
        openCameraResultLauncher = RegisterForActivityResult(new ActivityResultContracts.StartActivityForResult(), this);
    }

    Object IFlowCollector.Emit(Object value, IContinuation p1)
    {
        var uiState = value as UiState;

        FindViewById<LinearLayoutCompat>(Resource.Id.ll_initialize_section).Visibility =
            (uiState.initialized) ? ViewStates.Gone : ViewStates.Visible;
        FindViewById<LinearLayoutCompat>(Resource.Id.ll_generate_section).Visibility =
            (uiState.initialized) ? ViewStates.Visible : ViewStates.Gone;
        FindViewById<LinearLayoutCompat>(Resource.Id.ll_display_iteration).Visibility =
            (uiState.displayOptions == DisplayOptions.Iteration) ? ViewStates.Visible : ViewStates.Gone;

        // Button initialize is enabled when (the display option is final or iteration and display iteration is not null) and is not initializing
        FindViewById<Button>(Resource.Id.btn_initialize).Enabled =
            (uiState.displayOptions == DisplayOptions.Final || (uiState.displayOptions == DisplayOptions.Iteration && uiState.displayIteration != 0)) && !uiState.isInitializing;

        var btnGenerate = FindViewById<Button>(Resource.Id.btn_generate);
        if (uiState.isGenerating)
        {
            btnGenerate.Enabled = false;
            btnGenerate.Text = uiState.generatingMessage;
            FindViewById<TextView>(Resource.Id.tvDisclaimer).Visibility = ViewStates.Visible;
        }
        else
        {
            btnGenerate.Text = "Generate";
            if (uiState.initialized)
            {
                btnGenerate.Enabled =
                    !string.IsNullOrEmpty(uiState.prompt) && uiState.iteration != 0 && uiState.seed != 0;
            }
            else
            {
                btnGenerate.Enabled = false;
            }
        }
        FindViewById<ImageView>(Resource.Id.imgOutput).SetImageBitmap(uiState.outputBitmap);
        FindViewById<ImageView>(Resource.Id.imgDisplayInput).SetImageBitmap(uiState.inputBitmap);

        ShowError(uiState.error);
        ShowGenerateTime(uiState.generateTime);
        ShowInitializedTime(uiState.initializedTime);

        return uiState;
    }

    override protected void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_plugin);
        viewModel.CreateImageGenerationHelper(this);

        // Set up spinner
        var adapter = ArrayAdapter.CreateFromResource(
            this, Resource.Array.plugins, Android.Resource.Layout.SimpleSpinnerItem);
        adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
        FindViewById<Spinner>(Resource.Id.spinner_plugins).Adapter = adapter;

        GetLifecycleScope(this).Launch(() =>
        {
            RepeatOnLifecycle(this, Lifecycle.State.Started,
                new Function2(() =>
            {
                // Update UI
                viewModel.uiState.Collect(this, this);
            }), this);
        });

        HandleListener();
        SetDefaultValue();
    }

    void View.IOnClickListener.OnClick(View v)
    {
        if (v.Id == Resource.Id.btn_initialize)
        {
            viewModel.InitializeImageGenerator();
            var edtPrompt = FindViewById<EditText>(Resource.Id.edt_prompt);
            if (((UiState)viewModel.uiState.Value).plugins ==
                ImageGenerator.ConditionOptions.ConditionType.Face)
                    edtPrompt.SetText(Resource.String.default_prompt_plugin_face);
            if (((UiState)viewModel.uiState.Value).plugins ==
                ImageGenerator.ConditionOptions.ConditionType.Edge)
                    edtPrompt.SetText(Resource.String.default_prompt_plugin_edge);
            if (((UiState)viewModel.uiState.Value).plugins ==
                ImageGenerator.ConditionOptions.ConditionType.Depth)
                    edtPrompt.SetText(Resource.String.default_prompt_plugin_depth);
        }

        if (v.Id == Resource.Id.btn_generate)
            viewModel.GenerateImage();

        if (v.Id == Resource.Id.btn_open_camera)
            OpenCamera();

        if (v.Id == Resource.Id.btn_open_gallery)
            OpenGallery();

        if (v.Id == Resource.Id.btn_seed_random)
            RandomSeed();

        CloseSoftKeyboard();
    }

    void RadioGroup.IOnCheckedChangeListener.OnCheckedChanged(RadioGroup group, int checkedId)
    {
        switch(checkedId)
        {
            case Resource.Id.radio_iteration:
                viewModel.UpdateDisplayOptions(DisplayOptions.Iteration);
                break;

            case Resource.Id.radio_final:
                viewModel.UpdateDisplayOptions(DisplayOptions.Final);
                break;
        }
    }

    public void OnItemSelected(AdapterView parent, View view, int position, long id)
    {
        viewModel.UpdatePlugin(position);
    }

    public void OnNothingSelected(AdapterView parent)
    {
        // do nothing
    }

    public class TextWatcher : Object, Android.Text.ITextWatcher
    {
        PluginViewModel viewModel;
        int id;
        public TextWatcher(PluginViewModel viewModel, int id)
        {
            this.viewModel = viewModel;
            this.id = id;
        }

        void ITextWatcher.AfterTextChanged(IEditable s) { }

        void ITextWatcher.BeforeTextChanged(Java.Lang.ICharSequence s, int start, int count, int after) { }

        void ITextWatcher.OnTextChanged(Java.Lang.ICharSequence s, int start, int before, int count)
        {
            var text = s.ToString();

            if (id == Resource.Id.edt_display_iteration)
                viewModel.UpdateDisplayIteration(string.IsNullOrEmpty(text) ?
                    0 : Convert.ToInt32(text));

            if (id == Resource.Id.edt_prompt)
                viewModel.UpdatePrompt(text);

            if (id == Resource.Id.edt_iterations)
                viewModel.UpdateIteration(string.IsNullOrEmpty(text) ?
                    0 : Convert.ToInt32(text));

            if (id == Resource.Id.edt_seed)
                viewModel.UpdateSeed(string.IsNullOrEmpty(text) ?
                    0 : Convert.ToInt32(text));
        }
    }

    private void HandleListener()
    {
        FindViewById<Button>(Resource.Id.btn_initialize).SetOnClickListener(this);

        FindViewById<Button>(Resource.Id.btn_generate).SetOnClickListener(this);

        FindViewById<Button>(Resource.Id.btn_open_camera).SetOnClickListener(this);

        FindViewById<Button>(Resource.Id.btn_open_gallery).SetOnClickListener(this);

        FindViewById<Button>(Resource.Id.btn_seed_random).SetOnClickListener(this);

        FindViewById<RadioGroup>(Resource.Id.radioDisplayOptions).SetOnCheckedChangeListener(this);

        FindViewById<Spinner>(Resource.Id.spinner_plugins).OnItemSelectedListener = this;

        FindViewById<EditText>(Resource.Id.edt_display_iteration).AddTextChangedListener(
            new TextWatcher(viewModel, Resource.Id.edt_display_iteration));

        FindViewById<EditText>(Resource.Id.edt_prompt).AddTextChangedListener(
            new TextWatcher(viewModel, Resource.Id.edt_prompt));

        FindViewById<EditText>(Resource.Id.edt_iterations).AddTextChangedListener(
            new TextWatcher(viewModel, Resource.Id.edt_iterations));

        FindViewById<EditText>(Resource.Id.edt_seed).AddTextChangedListener(
            new TextWatcher(viewModel, Resource.Id.edt_seed));
    }

    private void ShowError(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        RunOnUiThread(() =>
        {
            Toast.MakeText(this, message, ToastLength.Short).Show();
            Log.Error("PluginActivity", "ImgGen Error" + message);
        });
        // prevent showing error message twice
        viewModel.ClearError();
    }

    private void ShowGenerateTime(long? time)
    {
        if (time == 0) return;
        RunOnUiThread(() =>
        {
            Toast.MakeText(
                this,
                "Generation time: " + time / 1000.0 + " seconds",
                ToastLength.Short
            ).Show();
        });
        // prevent showing generate time twice
        viewModel.ClearGenerateTime();
    }

    private void ShowInitializedTime(long? time)
    {
        if (time == 0) return;
        RunOnUiThread(() =>
        {
            Toast.MakeText(
                this,
                "Initialized time: " + time / 1000.0 + " seconds",
                ToastLength.Short
            ).Show();
        });
        // prevent showing initialized time twice
        viewModel.ClearInitializedTime();
    }

    private void CloseSoftKeyboard()
    {
        var imm = GetSystemService(InputMethodService) as InputMethodManager;
        imm.HideSoftInputFromWindow(Window.DecorView.RootView.WindowToken, 0);
    }

    private void OpenGallery()
    {
        var intent = new Intent(Intent.ActionGetContent);
        intent.SetType("image/*");
        openGalleryResultLauncher.Launch(intent);
    }

    private void OpenCamera()
    {
        var intent = new Intent(MediaStore.ActionImageCapture);
        openCameraResultLauncher.Launch(intent);
    }

    private void SetDefaultValue()
    {
        FindViewById<EditText>(Resource.Id.edt_iterations).Text = DefaultIteration.ToString();
        FindViewById<EditText>(Resource.Id.edt_seed).Text = DefaultSeed.ToString();
        FindViewById<RadioGroup>(Resource.Id.radioDisplayOptions).Check(DefaultDisplayOptions);
        FindViewById<EditText>(Resource.Id.edt_display_iteration).Text = DefaultDisplayIteration.ToString();
    }

    private void RandomSeed()
    {
        var random = new Random();
        var seed = Math.Abs(random.NextInt());
        FindViewById<EditText>(Resource.Id.edt_seed).Text = seed.ToString();
    }

    private Bitmap CropBitmapToSquare(Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var cropSize = (width > height) ? height : width;
        return Bitmap.CreateScaledBitmap(bitmap, cropSize, cropSize, false);
    }

    override protected void OnDestroy()
    {
        base.OnDestroy();
        viewModel.CloseGenerator();
    }
}
