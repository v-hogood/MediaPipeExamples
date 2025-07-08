using Android.Content;
using Android.Graphics;
using Android.OS;
using AndroidX.Lifecycle;
using Java.Lang;
using MediaPipe.Framework.Image;
using Xamarin.KotlinX.Coroutines;
using Xamarin.KotlinX.Coroutines.Flow;
using static MediaPipe.Framework.Image.BitmapImageBuilder;
using static MediaPipe.Tasks.Vision.ImageGenerator.ImageGenerator.ConditionOptions;
using static Xamarin.KotlinX.Coroutines.Flow.StateFlowKt;
using Exception = Java.Lang.Exception;
using Object = Java.Lang.Object;

namespace ImageGeneration;

public class PluginViewModel : ViewModel
{
    public IMutableStateFlow uiState = MutableStateFlow(new UiState());
    private ImageGenerationHelper helper;
    private string MODEL_PATH = "/data/local/tmp/image_generator/bins/";

    public void UpdateDisplayIteration(int displayIteration)
    {
        uiState.Update((it) => new UiState((UiState)it) { displayIteration = displayIteration });
    }
    
    public void UpdateDisplayOptions(DisplayOptions displayOptions)
    {
        uiState.Update((it) => new UiState((UiState)it) { displayOptions = displayOptions });
    }

    public void UpdatePrompt(string prompt)
    {
        uiState.Update((it) => new UiState((UiState)it) { prompt = prompt });
    }

    public void UpdateIteration(int iteration)
    {
        uiState.Update((it) => new UiState((UiState)it) { iteration = iteration });
    }

    public void UpdateSeed(int seed)
    {
        uiState.Update((it) => new UiState((UiState)it) { seed = seed });
    }

    public void UpdatePlugin(int plugin)
    {
        uiState.Update((it) =>
            new UiState((UiState)it)
            {
                plugins = plugin switch
                {
                    0 => ConditionType.Face,
                    1 => ConditionType.Depth,
                    2 => ConditionType.Edge,
                    _ => throw new IllegalArgumentException("Invalid plugin")
                }
            });
    }

    public void UpdateInputBitmap(Bitmap bitmap)
    {
        uiState.Update((it) => new UiState((UiState)it)
            { inputBitmap = Bitmap.CreateBitmap(bitmap) });
    }

    public void ClearError()
    {
        uiState.Update((it) => new UiState((UiState)it) { error = null });
    }

    public void ClearGenerateTime()
    {
        uiState.Update((it) => new UiState((UiState)it) { generateTime = 0 });
    }

    public void ClearInitializedTime()
    {
        uiState.Update((it) => new UiState((UiState)it) { initializedTime = 0 });
    }

    public void InitializeImageGenerator()
    {
        var displayIteration = ((UiState)uiState.Value).displayIteration;
        var displayOptions = ((UiState)uiState.Value).displayOptions;
        var conditionType = ((UiState)uiState.Value).plugins;
        try
        {
            if (displayIteration == 0 && displayOptions == DisplayOptions.Iteration)
            {
                uiState.Update((it) => new UiState((UiState)it) { error = "Display iteration cannot be empty" });
                return;
            }

            uiState.Update((it) => new UiState((UiState)it) { isInitializing = true });
            var mainLooper = Looper.MainLooper;
            GlobalScope.Instance.Launch(() =>
            {
                var startTime = JavaSystem.CurrentTimeMillis();
                if (conditionType == ConditionType.Face)
                    helper?.InitializeImageGeneratorWithFacePlugin(MODEL_PATH);
                if (conditionType == ConditionType.Edge)
                    helper?.InitializeImageGeneratorWithEdgePlugin(MODEL_PATH);
                if (conditionType == ConditionType.Depth)
                    helper?.InitializeImageGeneratorWithDepthPlugin(MODEL_PATH);

                new Handler(mainLooper).Post(() =>
                {
                    uiState.Update((it) => new UiState((UiState)it)
                    {
                        initialized = true,
                        isInitializing = false,
                        initializedTime = JavaSystem.CurrentTimeMillis() - startTime,
                    });
                });
            });
        }
        catch (Exception e)
        {
            uiState.Update(
                (it) => new UiState((UiState)it)
                    { error = e.Message ??
                        "Error initializing image generation model" });
        }
    }

    // Create image generation helper
    public void CreateImageGenerationHelper(Context context)
    {
        helper = new ImageGenerationHelper(context);
    }

    public void GenerateImage()
    {
        var prompt = ((UiState)uiState.Value).prompt;
        var iteration = ((UiState)uiState.Value).iteration;
        var seed = ((UiState)uiState.Value).seed;
        var displayIteration = ((UiState)uiState.Value).displayIteration;
        var inputImage = ((UiState)uiState.Value).inputBitmap;
        var conditionType = ((UiState)uiState.Value).plugins;
        var isDisplayStep = false;

        if (string.IsNullOrEmpty(prompt))
        {
            uiState.Update((it) => new UiState((UiState)it) { error = "Prompt cannot be empty" });
            return;
        }
        if (iteration == 0)
        {
            uiState.Update((it) => new UiState((UiState)it) { error = "Iteration cannot be empty" });
            return;
        }
        if (inputImage == null)
        {
            uiState.Update((it) => new UiState((UiState)it) { error = "Input image cannot be empty" });
            return;
        }

        uiState.Update((it) =>
            new UiState((UiState)it)
            {
                generatingMessage = "Generating...",
                isGenerating = true
            });

        // Generate with iterations
        GlobalScope.Instance.Launch(() =>
        {
            var startTime = JavaSystem.CurrentTimeMillis();

            // if display option is final, use generate method, else use execute method
            if (((UiState)uiState.Value).displayOptions == DisplayOptions.Final)
            {
                var result = helper?.Generate(
                    prompt,
                    new BitmapImageBuilder(inputImage).Build(),
                    conditionType,
                    iteration,
                    seed);
                uiState.Update((it) =>
                    new UiState((UiState)it) { outputBitmap = Bitmap.CreateBitmap(result) });
            }
            else
            {
                helper?.SetInput(
                    prompt,
                    new BitmapImageBuilder(inputImage).Build(),
                    conditionType,
                    iteration,
                    seed);
                for (int step = 0; step < iteration; step++)
                {
                    isDisplayStep =
                        (displayIteration > 0 && ((step + 1) % displayIteration == 0));
                    var result = helper?.Execute(isDisplayStep);

                    if (isDisplayStep)
                    {
                        uiState.Update((it) =>
                            new UiState((UiState)it)
                            {
                                outputBitmap = result,
                                generatingMessage = "Generating... (${step + 1}/$iteration)"
                            });
                    }
                }
            }
            uiState.Update((it) =>
                new UiState((UiState)it)
                {
                    isGenerating = false,
                    generatingMessage = "Generate",
                    generateTime = JavaSystem.CurrentTimeMillis() - startTime,
                });
        });
    }

    public void CloseGenerator()
    {
        helper?.Close();
    }

    public sealed class UiState : Object
    {
        public UiState() { }

        public UiState(UiState state)
        {
            error = state.error;
            inputBitmap = state.inputBitmap;
            outputBitmap = state.outputBitmap;
            displayOptions = state.displayOptions;
            plugins = state.plugins;
            displayIteration = state.displayIteration;
            prompt = state.prompt;
            iteration = state.iteration;
            seed = state.seed;
            initialized = state.initialized;
            initializedOutputSize = state.initializedOutputSize;
            initializedDisplayIteration = state.initializedDisplayIteration;
            isGenerating = state.isGenerating;
            isInitializing = state.isInitializing;
            generatingMessage = state.generatingMessage;
            generateTime = state.generateTime;
            initializedTime = state.initializedTime;
        }

        public string error = null;
        public Bitmap inputBitmap = null;
        public Bitmap outputBitmap = null;
        public DisplayOptions displayOptions = DisplayOptions.Final;
        public ConditionType plugins = ConditionType.Face;
        public int displayIteration = 0;
        public string prompt = "";
        public int iteration = 0;
        public int seed = 0;
        public bool initialized = false;
        public int initializedOutputSize = 0;
        public int initializedDisplayIteration = 0;
        public bool isGenerating = false;
        public bool isInitializing = false;
        public string generatingMessage = "";
        public long generateTime = 0;
        public long initializedTime = 0;
    }

    public enum DisplayOptions
    {
        Iteration,
        Final
    }
}
