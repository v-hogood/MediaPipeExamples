using Android.Content;
using Android.Graphics;
using Android.OS;
using AndroidX.Lifecycle;
using Java.Lang;
using Xamarin.KotlinX.Coroutines;
using Xamarin.KotlinX.Coroutines.Flow;
using static Xamarin.KotlinX.Coroutines.Flow.StateFlowKt;
using Exception = Java.Lang.Exception;
using Object = Java.Lang.Object;

namespace ImageGeneration;

public class LoRAWeightViewModel : ViewModel
{
    public IMutableStateFlow uiState = MutableStateFlow(new UiState());
    private ImageGenerationHelper helper;
    private string MODEL_PATH = "/data/local/tmp/image_generator/bins/";
    private string WEIGHT_PATH =
        "/data/local/tmp/image_generator/weights/teapot_lora.task";

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
                helper?.InitializeLoRAWeightGenerator(MODEL_PATH, WEIGHT_PATH);
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
        if (seed == 0)
        {
            uiState.Update((it) => new UiState((UiState)it) { error = "Seed cannot be empty" });
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
                var result = helper?.Generate(prompt, iteration, seed);
                uiState.Update((it) =>
                    new UiState((UiState)it) { outputBitmap = Bitmap.CreateBitmap(result) });
            }
            else
            {
                helper?.SetInput(prompt, iteration, seed);
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
            outputBitmap = state.outputBitmap;
            displayOptions = state.displayOptions;
            displayIteration = state.displayIteration;
            prompt = state.prompt;
            iteration = state.iteration;
            seed = state.seed;
            initialized = state.initialized;
            initializedDisplayIteration = state.initializedDisplayIteration;
            isGenerating = state.isGenerating;
            isInitializing = state.isInitializing;
            generatingMessage = state.generatingMessage;
            generateTime = state.generateTime;
            initializedTime = state.initializedTime;
        }

        public string error = null;
        public Bitmap outputBitmap = null;
        public DisplayOptions displayOptions = DisplayOptions.Final;
        public int displayIteration = 0;
        public string prompt = "";
        public int iteration = 0;
        public int seed = 0;
        public bool initialized = false;
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
