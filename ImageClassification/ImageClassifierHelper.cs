using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Util;
using AndroidX.Camera.Core;
using Java.Lang;
using MediaPipe.Framework.Image;
using MediaPipe.Tasks.Core;
using MediaPipe.Tasks.Vision.Core;
using MediaPipe.Tasks.Vision.ImageClassifier;
using static MediaPipe.Tasks.Core.OutputHandler;
using Object = Java.Lang.Object;
using Uri = Android.Net.Uri;

namespace ImageClassification;

public class ImageClassifierHelper : Object,
    IResultListener,
    IErrorListener,
    ImageAnalysis.IAnalyzer
{
    public float Threshold = ThresholdDefault;
    public int MaxResults = MaxResultsDefault;
    public int CurrentDelegate = DelegateCpu;
    public int CurrentModel = ModelEfficientnetV0;
    RunningMode runningMode = RunningMode.Image;

    Context context;
    IClassifierListener imageClassifierListener;

    // For this example this needs to be a var so it can be reset on changes. If the ImageClassifier
    // will not change, a lazy val would be preferable.
    private ImageClassifier imageClassifier = null;

    public ImageClassifierHelper(
        Context context,
        RunningMode runningMode,
        float threshold,
        int maxResults,
        int currentDelegate,
        int currentModel,
        IClassifierListener imageClassifierListener)
    {
        this.context = context;
        this.runningMode = runningMode;
        this.Threshold = threshold;
        this.MaxResults = maxResults;
        this.CurrentDelegate = currentDelegate;
        this.CurrentModel = currentModel;
        this.imageClassifierListener = imageClassifierListener;

        SetupImageClassifier();
    }

    // Classifier must be closed when creating a new one to avoid returning results to a
    // non-existent object
    public void ClearImageClassifier()
    {
        imageClassifier?.Close();
        imageClassifier = null;
    }

    // Return running status of image classifier helper
    public bool IsClosed() => imageClassifier == null;

    // Initialize the image classifier using current settings on the
    // thread that is using it. CPU can be used with detectors
    // that are created on the main thread and used on a background thread, but
    // the GPU delegate needs to be used on the thread that initialized the
    // classifier
    public void SetupImageClassifier()
    {
        var baseOptionsBuilder = BaseOptions.InvokeBuilder();
        switch (CurrentDelegate)
        {
            case DelegateCpu:
                baseOptionsBuilder.SetDelegate(Delegates.Cpu);
                break;
            case DelegateGpu:
                baseOptionsBuilder.SetDelegate(Delegates.Gpu);
                break;
        }

        var modelName =
            CurrentModel switch
            {
                ModelEfficientnetV0 => "efficientnet-lite0.tflite",
                ModelEfficientnetV2 => "efficientnet-lite2.tflite",
                _ => "mobilenetv0.tflite"
            };

        baseOptionsBuilder.SetModelAssetPath(modelName);

        // Check if runningMode is consistent with imageClassifierListener
        if (runningMode == RunningMode.LiveStream &&
            imageClassifierListener == null)
        {
            throw new IllegalStateException(
                "imageClassifierListener must be set when runningMode is LIVE_STREAM."
            );
        }

        try
        {
            var baseOptions = baseOptionsBuilder.Build();
            var optionsBuilder =
                ImageClassifier.ImageClassifierOptions.InvokeBuilder()
                    .SetScoreThreshold((Float)Threshold)
                    .SetMaxResults((Integer)MaxResults)
                    .SetRunningMode(runningMode)
                    .SetBaseOptions(baseOptions);

            if (runningMode == RunningMode.LiveStream)
            {
                optionsBuilder.SetResultListener(this);
                optionsBuilder.SetErrorListener(this);
            }
            var options = optionsBuilder.Build();
            imageClassifier =
                ImageClassifier.CreateFromOptions(context, options);
        }
        catch (IllegalStateException e)
        {
            imageClassifierListener?.OnError(
                "Image classifier failed to initialize. See error logs for details"
            );
            Log.Error(
                Tag,
                "Image classifier failed to load model with error: " + e.Message
            );
        }
        catch (RuntimeException e)
        {
            // This occurs if the model being used does not support GPU
            imageClassifierListener?.OnError(
                "Image classifier failed to initialize. See error logs for " +
                    "details", GpuError
            );
            Log.Error(
                Tag,
                "Image classifier failed to load model with error: " + e.Message
            );
        }
    }

    // Runs image classification on live streaming cameras frame-by-frame and
    // returns the results asynchronously to the caller.
    void ImageAnalysis.IAnalyzer.Analyze(IImageProxy imageProxy)
    {
        if (runningMode != RunningMode.LiveStream)
        {
            throw new IllegalArgumentException(
                "Attempting to call classifyLiveStreamFrame" +
                    " while not using RunningMode.LIVE_STREAM"
            );
        }

        var frameTime = SystemClock.UptimeMillis();
        var bitmapBuffer =
            Bitmap.CreateBitmap(
                imageProxy.Width,
                imageProxy.Height,
                Bitmap.Config.Argb8888
            );

        bitmapBuffer.CopyPixelsFromBuffer(imageProxy.GetPlanes()[0].Buffer);
        imageProxy.Close();
        var mpImage = new BitmapImageBuilder(bitmapBuffer).Build();
        ClassifyAsync(mpImage, imageProxy.ImageInfo.RotationDegrees, frameTime);
    }

    // Run object detection using MediaPipe Object Detector API
    void ClassifyAsync(MPImage mpImage, int imageDegree, long frameTime)
    {
        var imageProcessingOptions =
            ImageProcessingOptions.InvokeBuilder().SetRotationDegrees(imageDegree)
                .Build();
        // As we're using running mode LIVE_STREAM, the classification result will
        // be returned in returnLivestreamResult function
        imageClassifier?.ClassifyAsync(
            mpImage,
            imageProcessingOptions,
            frameTime
        );
    }

    // Accepted a Bitmap and runs image classification inference on it to
    // return results back to the caller
    public ResultBundle? ClassifyImage(Bitmap image)
    {
        if (runningMode != RunningMode.Image)
        {
            throw new IllegalArgumentException(
                "Attempting to call classifyImage" +
                    " while not using RunningMode.IMAGE"
            );
        }

        if (imageClassifier == null) return null;

        // Inference time is the difference between the system time at the start and finish of the
        // process
        var startTime = SystemClock.UptimeMillis();

        // Convert the input Bitmap object to an MPImage object to run inference
        var mpImage = new BitmapImageBuilder(image).Build();

        // Run image classification using MediaPipe Image Classifier API
        var classificationResults = imageClassifier?.Classify(mpImage);
        if (classificationResults != null)
        {
            var inferenceTimeMs = SystemClock.UptimeMillis() - startTime;
            return new ResultBundle
            {
                Results = new List<ImageClassifierResult> { classificationResults },
                InferenceTime = inferenceTimeMs
            };
        }

        // If imageClassifier?.classify() returns null, this is likely an error. Returning null
        // to indicate this.
        imageClassifierListener?.OnError(
            "Image classifier failed to classify."
        );
        return null;
    }

    // Accepts the URI for a video file loaded from the user's gallery and attempts to run
    // image classification inference on the video. This process will evaluate
    // every frame in the video and attach the results to a bundle that will
    // be returned.
    public ResultBundle? ClassifyVideoFile(
        Uri videoUri,
        long inferenceIntervalMs)
    {
        if (runningMode != RunningMode.Video)
        {
            throw new IllegalArgumentException(
                "Attempting to call classifyVideoFile" +
                    " while not using RunningMode.VIDEO"
            );
        }

        if (imageClassifier == null) return null;

        // Inference time is the difference between the system time at the start and finish of the
        // process
        var startTime = SystemClock.UptimeMillis();

        var didErrorOccurred = false;

        // Load frames from the video and run the image classification model.
        var retriever = new MediaMetadataRetriever();
        retriever.SetDataSource(context, videoUri);
        var duration = retriever.ExtractMetadata(MetadataKey.Duration);
        long? videoLengthMs = duration == null ? null : Long.ParseLong(duration);

        // Note: We need to read width/height from frame instead of getting the width/height
        // of the video directly because MediaRetriever returns frames that are smaller than the
        // actual dimension of the video file.
        var firstFrame = retriever.GetFrameAtTime(0);
        var width = firstFrame?.Width;
        var height = firstFrame?.Height;

        // If the video is invalid, returns a null classification result
        if ((videoLengthMs == null) || (width == null) || (height == null)) return null;

        // Next, we'll get one frame every frameInterval ms, then run
        // classification on these frames.
        var resultList = new List<ImageClassifierResult>();
        var numberOfFrameToRead = videoLengthMs / inferenceIntervalMs;

        for (int i = 0; i < numberOfFrameToRead; i++)
        {
            var timestampMs = i * inferenceIntervalMs; // ms

            var frame = retriever
                .GetFrameAtTime(
                    timestampMs * 1000, // convert from ms to micro-s
                    Option.Closest
                );
            if (frame != null)
            {
                // Convert the video frame to ARGB_8888 which is required by the MediaPipe
                var argb8888Frame =
                    frame.GetConfig() == Bitmap.Config.Argb8888 ? frame :
                    frame.Copy(Bitmap.Config.Argb8888, false);

                // Convert the input Bitmap object to an MPImage object to run inference
                var mpImage = new BitmapImageBuilder(argb8888Frame).Build();

                // Run image classification using MediaPipe Image Classifier
                // API
                var classificationResult =
                    imageClassifier?.ClassifyForVideo(mpImage, timestampMs);
                if (classificationResult != null)
                {
                    resultList.Add(classificationResult);
                }
                else
                {
                    didErrorOccurred = true;
                    imageClassifierListener?.OnError(
                        "ResultBundle could not be " +
                            "returned" +
                            " in classifyVideoFile"
                    );
                }
            }
            else
            {
                didErrorOccurred = true;
                imageClassifierListener?.OnError(
                    "Frame at specified time could not be" +
                        " retrieved when classifying in video."
                );
            }
        }

        retriever.Release();

        var inferenceTimePerFrameMs =
            (SystemClock.UptimeMillis() - startTime) / numberOfFrameToRead;

        return didErrorOccurred ? null :
            new ResultBundle
            {
                Results = resultList,
                InferenceTime = inferenceTimePerFrameMs ?? 0
            };
    }

    // MPImage isn't necessary for this example, but the listener requires it
    public void Run(
        Object resobj,
        Object image)
    {
        var result = resobj as ImageClassifierResult;

        var finishTimeMs = SystemClock.UptimeMillis();

        var inferenceTime = finishTimeMs - result.TimestampMs();

        imageClassifierListener?.OnResults(
            new ResultBundle
            {
                Results = new List<ImageClassifierResult> { result },
                InferenceTime = inferenceTime
            }
        );
    }

    // Return errors thrown during classification to this
    // ImageClassifierHelper's caller
    public void OnError(RuntimeException error)
    {
        imageClassifierListener?.OnError(
            error?.Message ?? "An unknown error has occurred"
        );
    }

    // Wraps results from inference, the time it takes for inference to be
    // performed.
    public struct ResultBundle
    {
        public List<ImageClassifierResult> Results;
        public long InferenceTime;
    }

    public const int DelegateCpu = 0;
    public const int DelegateGpu = 1;
    public const int ModelEfficientnetV0 = 0;
    public const int ModelEfficientnetV2 = 1;
    public const int MaxResultsDefault = 3;
    public const float ThresholdDefault = 0.5f;
    public const int OtherError = 0;
    public const int GpuError = 1;

    private const string Tag = "ImageClassifierHelper";

    public interface IClassifierListener
    {
        void OnError(string error, int errorCode = OtherError);
        void OnResults(ResultBundle resultBundle);
    }
}
