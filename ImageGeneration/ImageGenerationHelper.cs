using Android.Content;
using Android.Graphics;
using MediaPipe.Framework.Image;
using MediaPipe.Tasks.Core;
using MediaPipe.Tasks.Vision.ImageGenerator;
using static MediaPipe.Tasks.Vision.ImageGenerator.ImageGenerator;
using static MediaPipe.Tasks.Vision.ImageGenerator.ImageGenerator.ConditionOptions;
using Exception = Java.Lang.Exception;
using Object = Java.Lang.Object;

namespace ImageGeneration;

public class ImageGenerationHelper : Object
{
    Context context;
    public ImageGenerationHelper(Context context) => this.context = context;

    ImageGenerator imageGenerator;

    // Setup image generation model with output size, iteration
    public void InitializeImageGenerator(string modelPath)
    {
        var options = ImageGeneratorOptions.InvokeBuilder()
            .SetImageGeneratorModelDirectory(modelPath)
            .Build();

        imageGenerator = ImageGenerator.CreateFromOptions(context, options);
    }

    public void InitializeImageGeneratorWithFacePlugin(string modelPath)
    {
        var options = ImageGeneratorOptions.InvokeBuilder()
            .SetImageGeneratorModelDirectory(modelPath)
            .Build();

        var faceModelBaseOptions = BaseOptions.InvokeBuilder()
            .SetModelAssetPath("face_landmarker.task")
            .Build();

        var facePluginModelBaseOptions = BaseOptions.InvokeBuilder()
            .SetModelAssetPath("face_landmark_plugin.tflite")
            .Build();

        var faceConditionOptions = FaceConditionOptions.InvokeBuilder()
            .SetFaceModelBaseOptions(faceModelBaseOptions)
            .SetPluginModelBaseOptions(facePluginModelBaseOptions)
            .SetMinFaceDetectionConfidence(0.3f)
            .SetMinFacePresenceConfidence(0.3f)
            .Build();

        var conditionOptions = ConditionOptions.InvokeBuilder()
            .SetFaceConditionOptions(faceConditionOptions)
            .Build();

        imageGenerator =
            ImageGenerator.CreateFromOptions(context, options, conditionOptions);
    }

    public void InitializeImageGeneratorWithEdgePlugin(string modelPath)
    {
        var options = ImageGeneratorOptions.InvokeBuilder()
            .SetImageGeneratorModelDirectory(modelPath)
            .Build();

        var edgePluginModelBaseOptions = BaseOptions.InvokeBuilder()
            .SetModelAssetPath("canny_edge_plugin.tflite")
            .Build();

        var edgeConditionOptions = EdgeConditionOptions.InvokeBuilder()
            .SetThreshold1(100.0f) // default = 100.0f
            .SetThreshold2(100.0f) // default = 100.0f
            .SetApertureSize(3) // default = 3
            .SetL2Gradient(false) // default = false
            .SetPluginModelBaseOptions(edgePluginModelBaseOptions)
            .Build();

        var conditionOptions = ConditionOptions.InvokeBuilder()
            .SetEdgeConditionOptions(edgeConditionOptions)
            .Build();

        imageGenerator =
            ImageGenerator.CreateFromOptions(context, options, conditionOptions);
    }

    public void InitializeImageGeneratorWithDepthPlugin(string modelPath)
    {
        var options = ImageGeneratorOptions.InvokeBuilder()
            .SetImageGeneratorModelDirectory(modelPath)
            .Build();

        var depthModelBaseOptions = BaseOptions.InvokeBuilder()
            .SetModelAssetPath("depth_model.tflite")
            .Build();

        var depthPluginModelBaseOptions = BaseOptions.InvokeBuilder()
            .SetModelAssetPath("depth_plugin.tflite")
            .Build();

        var depthConditionOptions =
            ConditionOptions.DepthConditionOptions.InvokeBuilder()
                .SetDepthModelBaseOptions(depthModelBaseOptions)
                .SetPluginModelBaseOptions(depthPluginModelBaseOptions)
                .Build();

        var conditionOptions = ConditionOptions.InvokeBuilder()
            .SetDepthConditionOptions(depthConditionOptions)
            .Build();

        imageGenerator =
            ImageGenerator.CreateFromOptions(context, options, conditionOptions);
    }

    public void InitializeLoRAWeightGenerator(string modelPath, string weightsPath)
    {
        var options = ImageGeneratorOptions.InvokeBuilder()
            .SetLoraWeightsFilePath(weightsPath)
            .SetImageGeneratorModelDirectory(modelPath)
            .Build();

        imageGenerator = ImageGenerator.CreateFromOptions(context, options);
    }

    public void SetInput(
        string prompt,
        MPImage conditionalImage,
        ConditionType conditionType,
        int iteration,
        int seed)
    {
        imageGenerator.SetInputs(
            prompt,
            conditionalImage,
            conditionType,
            iteration,
            seed
        );
    }

    // Set input prompt, iteration, seed
    public void SetInput(string prompt, int iteration, int seed)
    {
        imageGenerator.SetInputs(prompt, iteration, seed);
    }

    public Bitmap Generate(string prompt, int iteration, int seed)
    {
        var result = imageGenerator.Generate(prompt, iteration, seed);
        var bitmap = BitmapExtractor.Extract(result?.GeneratedImage());
        return bitmap;
    }

    public Bitmap Generate(
        string prompt,
        MPImage inputImage,
        ConditionType conditionType,
        int iteration,
        int seed)
    {
        var result = imageGenerator.Generate(
            prompt,
            inputImage,
            conditionType,
            iteration,
            seed
        );
        var bitmap = BitmapExtractor.Extract(result?.GeneratedImage());
        return bitmap;
    }

    public Bitmap Execute(bool showResult)
    {
        // execute image generation model
        var result = imageGenerator.Execute(showResult);

        if (result == null || result.GeneratedImage() == null)
        {
            var retval = Bitmap.CreateBitmap(512, 512, Bitmap.Config.Argb8888);
            var canvas = new Canvas(retval);
            var paint = new Paint();
            paint.Color = Color.White;
            canvas.DrawPaint(paint);
            return retval;
        }

        var bitmap =
            BitmapExtractor.Extract(result.GeneratedImage());

        return bitmap;
    }

    Bitmap CreateConditionImage(
        MPImage inputImage,
        ConditionType conditionType)
    {
        return BitmapExtractor.Extract(imageGenerator.CreateConditionImage(inputImage, conditionType));
    }

    public void Close()
    {
        try
        {
            imageGenerator.Close();
        }
        catch (Exception e)
        {
            e.PrintStackTrace();
        }
    }
}
