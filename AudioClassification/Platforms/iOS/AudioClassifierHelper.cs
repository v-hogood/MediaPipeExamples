using AVFoundation;
using CoreAnimation;
using CoreFoundation;
using Foundation;
using MediaPipeTasksAudio;

namespace AudioClassification;

public interface IAudioClassifierHelperDelegate
{
    void DidFinishClassification(AudioClassifierHelper helper,
        List<MPPCategory> categories, double inferenceTime);
    void DidFailWithError(AudioClassifierHelper helper,
        NSError error);
}

public class AudioClassifierHelper : MPPAudioClassifierStreamDelegate
{
    public IAudioClassifierHelperDelegate Delegate;

    MPPAudioClassifier audioClassifier;
    MPPAudioRecord audioRecord;
    NSTimer timer;

    string modelPath;
    public float ScoreThreshold;
    public int MaxResults;
    private double sampleRate = 16000.0;
    private int channelCount = 1;
    private double lastInferenceStartTime = 0;

    public AudioClassifierHelper(string modelPath, float scoreThreshold = 0.3f, int maxResults = 3)
    {
        this.modelPath = modelPath;
        this.ScoreThreshold = scoreThreshold;
        this.MaxResults = maxResults;
    }

    public void StartLiveClassification()
    {
        AVAudioApplication.RequestRecordPermission((granted) =>
        {
            if (!granted)
            {
                this.Delegate?.DidFailWithError(this, new NSError(
                    domain: new NSString("AudioClassifierHelper"),
                    code: -1,
                    userInfo: new NSDictionary(NSError.LocalizedDescriptionKey, new NSString("Microphone permission was denied"))
                ));
            }
            return;
        });

        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            var session = AVAudioSession.SharedInstance();
            session.SetCategory(AVAudioSessionCategory.PlayAndRecord,
                mode: AVAudioSessionMode.Default,
                options: AVAudioSessionCategoryOptions.DefaultToSpeaker | AVAudioSessionCategoryOptions.AllowBluetooth);
            session.SetActive(true, outError: out NSError error);

            if (error == null)
            {
                var options = new MPPAudioClassifierOptions();
                options.BaseOptions.ModelAssetPath = this.modelPath;
                options.RunningMode = MPPAudioRunningMode.Stream;
                options.ScoreThreshold = this.ScoreThreshold;
                options.MaxResults = this.MaxResults;
                options.AudioClassifierStreamDelegate = this;

                this.audioClassifier = new MPPAudioClassifier(options: options, out error);

                if (error == null)
                {
                    var format = new MPPAudioDataFormat(channelCount: (nuint)this.channelCount, sampleRate: this.sampleRate);
                    var bufferLength = (nuint) this.sampleRate * 2;
                    this.audioRecord = new MPPAudioRecord(format: format, bufferSize: bufferLength, error: out error);

                    if (error == null)
                    {
                        this.audioRecord?.StartRecordingWithError(out error);

                        if (error == null)
                        {
                            // Classify audio every 500ms
                            this.timer = NSTimer.CreateScheduledTimer(interval: 0.5, repeats: true, (_) => {
                                this.ClassifyAsync();
                            });
                        }
                    }
                }
            }
            if (error != null)
            {
                this.Delegate?.DidFailWithError(this, error: error);
            }
      });
    }

    private void ClassifyAsync()
    {
        if (audioRecord == null || audioClassifier == null) return;
        var audioFormat = new MPPAudioDataFormat(
            channelCount: (nuint)channelCount, sampleRate: sampleRate);
        var sampleCount = (nuint)(sampleRate * 0.975);
        var audioData = new MPPAudioData(format: audioFormat, sampleCount: sampleCount);
        audioData.LoadAudioRecord(audioRecord: audioRecord, error: out NSError error);
        if (error == null)
        {
            lastInferenceStartTime = CAAnimation.CurrentMediaTime();
            var timestamp = (int)(CAAnimation.CurrentMediaTime() * 1000);
            audioClassifier.ClassifyAsyncAudioBlock(
                audioBlock: audioData, timestampInMilliseconds: timestamp, error: out error);
        }
        if (error != null)
        {
            Delegate?.DidFailWithError(this, error: error);
        }
    }

    public void StopLiveClassification()
    {
        timer?.Invalidate();
        timer = null;

        NSError error = null;
        audioRecord?.StopWithError(error: out error);
        audioRecord = null;
        if (error == null)
        {
            audioClassifier?.CloseWithError(error: out error);
            audioClassifier = null;
            AVAudioSession.SharedInstance().SetActive(false,
                options: AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation);
        }
        if (error != null)
        {
            Console.WriteLine("Error stopping audio classification: " + error);
        }
    }

    // MARK: - AudioClassifierStreamDelegate

    override public void DidFinishClassificationWithResult(
        MPPAudioClassifier audioClassifier,
        MPPAudioClassifierResult result,
        nint timestampInMilliseconds,
        NSError error)
    {
        if (error != null)
        {
            DispatchQueue.MainQueue.DispatchAsync(() =>
                this.Delegate?.DidFailWithError(this, error: error));
            return;
        }

        var inferenceTime = (CAAnimation.CurrentMediaTime() - lastInferenceStartTime) * 1000.0;
        var categories = new List<MPPCategory>();
        var classificationResult = result?.ClassificationResults[0];
        if (classificationResult != null)
        {
            var head = classificationResult.Classifications[0];
            categories = new List<MPPCategory>(head.Categories);
        }

        DispatchQueue.MainQueue.DispatchAsync(()=>
            this.Delegate?.DidFinishClassification(
                this,
                categories: categories,
                inferenceTime: Math.Max(1.0, inferenceTime)));
    }
}
