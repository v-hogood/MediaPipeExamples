using Android.Content;
using Android.Media;
using Android.OS;
using Android.Util;
using Java.Lang;
using Java.Util.Concurrent;
using MediaPipe.Tasks.Audio.AudioClassifier;
using MediaPipe.Tasks.Audio.Core;
using MediaPipe.Tasks.Components.Containers;
using MediaPipe.Tasks.Core;
using static MediaPipe.Tasks.Components.Containers.AudioData;
using Object = Java.Lang.Object;

namespace AudioClassification;

public class AudioClassifierHelper : Object,
    OutputHandler.IPureResultListener,
    IErrorListener
{
    private Context context;
    public float ClassificationThreshold = DisplayThreshold;
    public int Overlap = DefaultOverlap;
    public int NumOfResults = DefaultNumOfResults;
    private RunningMode runningMode = RunningMode.AudioClips;
    private IClassifierListener listener = null;

    private AudioRecord recorder = null;
    private ScheduledThreadPoolExecutor executor = null;
    private AudioClassifier audioClassifier = null;
    private Runnable classifyRunnable;

    public AudioClassifierHelper(
        Context context,
        float classificationThreshold,
        int overlap,
        int numOfResults,
        RunningMode runningMode,
        IClassifierListener listener)
    {
        this.context = context;
        this.ClassificationThreshold = classificationThreshold;
        this.Overlap = overlap;
        this.NumOfResults = numOfResults;
        this.runningMode = runningMode;
        this.listener = listener;

        classifyRunnable = new(() => ClassifyAudioAsync(recorder));

        InitClassifier();
    }

    public void InitClassifier()
    {
        // Set general detection options, e.g. number of used threads
        var baseOptionsBuilder = BaseOptions.InvokeBuilder();

        baseOptionsBuilder.SetModelAssetPath(YamnetModel);

        try
        {
            // Configures a set of parameters for the classifier and what results will be returned.
            var baseOptions = baseOptionsBuilder.Build();
            var optionsBuilder =
                AudioClassifier.AudioClassifierOptions.InvokeBuilder()
                    .SetScoreThreshold((Float)ClassificationThreshold)
                    .SetMaxResults((Integer)NumOfResults)
                    .SetBaseOptions(baseOptions)
                    .SetRunningMode(runningMode);

            if (runningMode == RunningMode.AudioStream)
            {
                optionsBuilder
                    .SetResultListener(this)
                    .SetErrorListener(this);
            }

            var options = optionsBuilder.Build();

            // Create the classifier and required supporting objects
            audioClassifier = AudioClassifier.CreateFromOptions(context, options);
            if (runningMode == RunningMode.AudioStream)
            {
                recorder = audioClassifier.CreateAudioRecord(
                    (int)ChannelIn.Default,
                    SamplingRateInHz,
                    BufferSizeInBytes);

                StartAudioClassification();
            }
        }
        catch (IllegalStateException e)
        {
            listener?.OnError(
                "Audio Classifier failed to initialize. See error logs for details"
            );

            Log.Error(
                Tag, "MP task failed to load with error: " + e.Message
            );
        }
        catch (RuntimeException e)
        {
            listener?.OnError(
                "Audio Classifier failed to initialize. See error logs for details"
            );

            Log.Error(
                Tag, "MP task failed to load with error: " + e.Message
            );
        }
    }

    public void StartAudioClassification()
    {
        if (recorder?.RecordingState == RecordState.Recording)
        {
            return;
        }

        recorder?.StartRecording();
        executor = new ScheduledThreadPoolExecutor(1);

        // Each model will expect a specific audio recording length. This formula calculates that
        // length using the input buffer size and tensor format sample rate.
        // For example, YAMNET expects 0.975 second length recordings.
        // This needs to be in milliseconds to avoid the required Long value dropping decimals.
        var lengthInMilliSeconds =
            ((float)RequiredInputBufferSize / SamplingRateInHz) * 1000;

        var interval = (long) (lengthInMilliSeconds * (1 - Overlap * 0.25F));

        executor.ScheduleAtFixedRate(
            classifyRunnable,
            0,
            interval,
            TimeUnit.Milliseconds);
    }

    private void ClassifyAudioAsync(AudioRecord audioRecord)
    {
        var audioData = AudioData.Create(
            AudioDataFormat.Create(recorder.Format), sampleCounts: SamplingRateInHz
        );
        audioData.Load(audioRecord);

        var inferenceTime = SystemClock.UptimeMillis();
        audioClassifier?.ClassifyAsync(audioData, inferenceTime);
    }

    public ResultBundle? ClassifyAudio(AudioData audioData)
    {
        var startTime = SystemClock.UptimeMillis();
        var audioClassificationResult = audioClassifier?.Classify(audioData);
        if (audioClassificationResult != null)
        {
            var inferenceTime = SystemClock.UptimeMillis() - startTime;
            return new ResultBundle
            {
                results = new() { audioClassificationResult },
                inferenceTime = inferenceTime
            };
        }

        // If audioClassifier?.classify() returns null, this is likely an error. Returning null
        // to indicate this.
        listener?.OnError("Audio classifier failed to classify.");
        return null;
    }

    public void StopAudioClassification()
    {
        executor?.Shutdown();
        audioClassifier?.Close();
        audioClassifier = null;
        recorder?.Stop();
    }

    public bool IsClosed() => audioClassifier == null;

    void OutputHandler.IPureResultListener.Run(Object result)
    {
        listener?.OnResult(new ResultBundle
        {
            results = new() { result as AudioClassifierResult },
            inferenceTime = 0
        });
    }

    void IErrorListener.OnError(RuntimeException e)
    {
        listener?.OnError(e.Message);
    }

    // Wraps results from inference, the time it takes for inference to be
    // performed.
    public struct ResultBundle
    {
        public List<AudioClassifierResult> results;
        public long inferenceTime;
    }

    private const string Tag = "AudioClassifierHelper";
    public const float DisplayThreshold = 0.3f;
    public const int DefaultNumOfResults = 2;
    public const int DefaultOverlap = 2;
    public const string YamnetModel = "yamnet.tflite";

    private const int SamplingRateInHz = 16000;
    private const int BufferSizeFactor = 2;
    public const float ExpectedInputLength = 0.975F;
    private const int RequiredInputBufferSize = (int)
        (SamplingRateInHz * ExpectedInputLength);

    //
    // Size of the buffer where the audio data is stored by Android
    //
    private const int BufferSizeInBytes =
        RequiredInputBufferSize * sizeof(float) * BufferSizeFactor;

    public interface IClassifierListener
    {
        void OnError(string error);
        void OnResult(ResultBundle resultBundle);
    }
}
