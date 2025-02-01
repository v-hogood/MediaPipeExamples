using Android.Text;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using AndroidX.Lifecycle;
using Kotlin.Coroutines;
using Xamarin.KotlinX.Coroutines.Flow;
using static AndroidX.Lifecycle.LifecycleOwnerKt;
using static AndroidX.Lifecycle.RepeatOnLifecycleKt;
using static ImageGeneration.BuildersKtx;
using static ImageGeneration.LoRAWeightViewModel;
using Object = Java.Lang.Object;
using Random = Java.Util.Random;

namespace ImageGeneration;

[Activity(Name = "com.google.mediapipe.examples.imagegeneration.loraweights.LoRAWeightActivity", Label = "@string/app_name", Theme = "@style/AppTheme")]
public class LoRAWeightActivity : AppCompatActivity,
    View.IOnClickListener,
    RadioGroup.IOnCheckedChangeListener,
    IFlowCollector,
    IContinuation
{
    private const int DefaultDisplayIteration = 5;
    private const int DefaultIteration = 20;
    private const int DefaultSeed = 0;
    private string DefaultPrompt;
    private int DefaultDisplayOptions = Resource.Id.radio_final; // FINAL

    private LoRAWeightViewModel viewModel = new();

    ICoroutineContext IContinuation.Context => GetLifecycleScope(this).CoroutineContext;

    public void ResumeWith(Object result) { }

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

        ShowError(uiState.error);
        ShowGenerateTime(uiState.generateTime);
        ShowInitializedTime(uiState.initializedTime);

        return uiState;
    }

    override protected void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_lora);
        DefaultPrompt = Resources.GetString(Resource.String.default_prompt_diffusion);
        viewModel.CreateImageGenerationHelper(this);

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
            viewModel.InitializeImageGenerator();

        if (v.Id == Resource.Id.btn_generate)
            viewModel.GenerateImage();

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

    public class TextWatcher : Object, Android.Text.ITextWatcher
    {
        LoRAWeightViewModel viewModel;
        int id;
        public TextWatcher(LoRAWeightViewModel viewModel, int id)
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

        FindViewById<Button>(Resource.Id.btn_seed_random).SetOnClickListener(this);

        FindViewById<RadioGroup>(Resource.Id.radioDisplayOptions).SetOnCheckedChangeListener(this);

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
            Log.Error("LoRAWeightActivity", "ImgGen Error" + message);
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

    private void SetDefaultValue()
    {
        FindViewById<EditText>(Resource.Id.edt_prompt).Text = DefaultPrompt;
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

    override protected void OnDestroy()
    {
        base.OnDestroy();
        viewModel.CloseGenerator();
    }
}
