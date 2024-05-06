using AVFoundation;
using CoreFoundation;
using CoreMedia;
using MediaPipeTasksVision;

namespace ImageClassification;

//
// This protocol must be adopted by any class that wants to get the classification results of the image classifier in live stream mode.
//
public interface ImageClassifierServiceLiveStreamDelegate
{
    void ImageClassifierService(ImageClassifierService imageClassifierService,
                                ResultBundle? result,
                                NSError error);
}

//
// This protocol must be adopted by any class that wants to take appropriate actions during  different stages of image classification on videos.
//
public interface ImageClassifierServiceVideoDelegate
{
    void ImageClassifierService(ImageClassifierService imageClassifierService,
                                int index,
                                NSError error);
    void ImageClassifierService(ImageClassifierService imageClassifierService,
                                int totalFrameCount);
}

// Initializes and calls the MediaPipe APIs for classification.
public class ImageClassifierService : MPPImageClassifierLiveStreamDelegate
{
    ImageClassifierServiceLiveStreamDelegate liveStreamDelegate;
    ImageClassifierServiceVideoDelegate videoDelegate;

    MPPImageClassifier imageClassifier;
    private MPPRunningMode runningMode;
    private float scoreThreshold;
    private int maxResult;
    private string modelPath;
    private ImageClassifierDelegate imageClassifierDelegate;

    public ImageClassifierService(
        Model model,
        float scoreThreshold,
        int maxResult,
        MPPRunningMode runningMode,
        ImageClassifierDelegate imageClassifierDelegate)
    {
        modelPath = model.ModelPath();
        if (modelPath == null) return;
        this.scoreThreshold = scoreThreshold;
        this.runningMode = runningMode;
        this.maxResult = maxResult;
        this.imageClassifierDelegate = imageClassifierDelegate;

        CreateImageClassifier();
    }

    private void CreateImageClassifier()
    {
        var imageClassifierOptions = new MPPImageClassifierOptions();
        imageClassifierOptions.RunningMode = runningMode;
        imageClassifierOptions.ScoreThreshold = scoreThreshold;
        imageClassifierOptions.MaxResults = maxResult;
        imageClassifierOptions.BaseOptions.ModelAssetPath = modelPath;
        imageClassifierOptions.BaseOptions.Delegate = imageClassifierDelegate.Delegate();
        if (runningMode == MPPRunningMode.LiveStream)
        {
            imageClassifierOptions.ImageClassifierLiveStreamDelegate = this;
        }
        NSError error;
        imageClassifier = new(options: imageClassifierOptions, error: out error);
        if (error != null)
        {
            Console.WriteLine(error);
        }
    }

    public static ImageClassifierService VideoClassifierService(
        Model model,
        float scoreThreshold,
        int maxResult,
        ImageClassifierServiceVideoDelegate videoDelegate,
        ImageClassifierDelegate imageClassifierDelegate)
    {
        var imageClassifierService = new ImageClassifierService(
            model: model,
            scoreThreshold: scoreThreshold,
            maxResult: maxResult,
            runningMode: MPPRunningMode.Video,
            imageClassifierDelegate: imageClassifierDelegate);
        imageClassifierService.videoDelegate = videoDelegate;

        return imageClassifierService;
    }

    public static ImageClassifierService LiveStreamClassifierService(
        Model model,
        float scoreThreshold,
        int maxResult,
        ImageClassifierServiceLiveStreamDelegate liveStreamDelegate,
        ImageClassifierDelegate imageClassifierDelegate)
    {
        var imageClassifierService = new ImageClassifierService(
            model: model,
            scoreThreshold: scoreThreshold,
            maxResult: maxResult,
            runningMode: MPPRunningMode.LiveStream,
            imageClassifierDelegate: imageClassifierDelegate);
        imageClassifierService.liveStreamDelegate = liveStreamDelegate;

        return imageClassifierService;
    }

    public static ImageClassifierService StillImageClassifierService(
        Model model,
        float scoreThreshold,
        int maxResult,
        ImageClassifierDelegate imageClassifierDelegate)
    {
        var imageClassifierService = new ImageClassifierService(
            model: model,
            scoreThreshold: scoreThreshold,
            maxResult: maxResult,
            runningMode: MPPRunningMode.Image,
            imageClassifierDelegate: imageClassifierDelegate);

        return imageClassifierService;
    }

    //
    // This method return ImageClassifierResult and infrenceTime when receive an image
    //
    public ResultBundle? Classify(UIImage image)
    {
        NSError error;
        var mppImage = new MPPImage(image: image, error: out error);
        if (mppImage == null)
            return null;
        var startDate = new NSDate();
        var result = imageClassifier?.ClassifyImage(image: mppImage, error: out error);
        if (error != null)
        {
            Console.WriteLine(error);
            return null;
        }
        var inferenceTime = new NSDate().GetSecondsSince(startDate) * 1000;
        return new() { inferenceTime = inferenceTime, imageClassifierResults = new[] { result } };
    }

    public void ClassifyAsync(
        CMSampleBuffer sampleBuffer,
        UIImageOrientation orientation,
        nint timeStamp)
    {
        NSError error;
        var image = new MPPImage(sampleBuffer: sampleBuffer, orientation: orientation,
            error: out error);
        if (image == null)
            return;
        imageClassifier?.ClassifyAsyncImage(image: image, timestampInMilliseconds: timeStamp,
            error: out error);
        if (error != null)
        {
            Console.WriteLine(error);
        }
        sampleBuffer.Dispose();
        // https://stackoverflow.com/questions/30850676/avcaptureoutput-didoutputsamplebuffer-stops-getting-called
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public ResultBundle? Classify(
        AVAsset videoAsset,
        double durationInMilliseconds,
        double inferenceIntervalInMilliseconds)
    {
        var startDate = new NSDate();
        var assetGenerator = ImageGenerator(videoAsset);

        var frameCount = (int)(durationInMilliseconds / inferenceIntervalInMilliseconds);
        DispatchQueue.MainQueue.DispatchAsync(() =>
            videoDelegate?.ImageClassifierService(this, totalFrameCount: frameCount));

        var imageClassifierResultTuple = ClassifyObjectsInFramesGenerated(
            assetGenerator: assetGenerator,
            frameCount: frameCount,
            inferenceIntervalMs: inferenceIntervalInMilliseconds);

        return new() {
            inferenceTime=  new NSDate().GetSecondsSince(startDate) / (double)frameCount * 1000,
            imageClassifierResults = imageClassifierResultTuple.Item1,
            size = imageClassifierResultTuple.Item2 };
    }

    private AVAssetImageGenerator ImageGenerator(AVAsset videoAsset)
    {
        var generator = new AVAssetImageGenerator(asset: videoAsset);
        generator.RequestedTimeToleranceBefore = new(value: 1, timescale: 25);
        generator.RequestedTimeToleranceAfter = new(value: 1, timescale: 25);
        generator.AppliesPreferredTrackTransform = true;

        return generator;
    }

    private (MPPImageClassifierResult[], CGSize) ClassifyObjectsInFramesGenerated(
        AVAssetImageGenerator assetGenerator,
        int frameCount,
        double inferenceIntervalMs)
    {
        var imageClassifierResults = new MPPImageClassifierResult[frameCount];
        var videoSize = CGSize.Empty;

        for (int i = 0; i < frameCount; i++)
        {
            var timestampMs = (int)inferenceIntervalMs * i; // ms
                CGImage image;

            var time = new CMTime(value: timestampMs, timescale: 1000);
            CMTime actualTime;
            NSError error;
#pragma warning disable CA1422
            image = assetGenerator.CopyCGImageAtTime(requestedTime: time, actualTime: out actualTime, outError: out error);
#pragma warning restore CA1422
            if (error != null)
            {
                Console.WriteLine(error);
                return (imageClassifierResults, videoSize);
            }

            var uiImage = new UIImage(cgImage: image);
            videoSize = uiImage.Size;

            var result = imageClassifier?.ClassifyVideoFrame(
                image: new(image: uiImage, error: out error),
                timestampInMilliseconds: timestampMs,
                error: out error);
            if (error != null)
            {
                Console.WriteLine(error);
                return (imageClassifierResults, videoSize);
            }
            imageClassifierResults[i] = result;
            DispatchQueue.MainQueue.DispatchAsync(() =>
                videoDelegate?.ImageClassifierService(this, index: i, error: error));
        }

        return (imageClassifierResults, videoSize);
    }

    override public void DidFinishClassificationWithResult(
        MPPImageClassifier imageClassifier,
        MPPImageClassifierResult result,
        IntPtr timestampInMilliseconds,
        NSError error)
    {
        if (result == null)
        {
            liveStreamDelegate?.ImageClassifierService(this, result: null, error: error);
            return;
        }

        var resultBundle = new ResultBundle()
        {
            inferenceTime = new NSDate().SecondsSince1970 * 1000 - (double)timestampInMilliseconds,
            imageClassifierResults = new[] { result }
        };
        liveStreamDelegate?.ImageClassifierService(this, result: resultBundle, error: null);
    }
}

// A result from inference, the time it takes for inference to be
// performed.
public struct ResultBundle
{
    public ResultBundle() { }
    public double inferenceTime;
    public MPPImageClassifierResult[] imageClassifierResults;
    public CGSize size = CGSize.Empty;
}
