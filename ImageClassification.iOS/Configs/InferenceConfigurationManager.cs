namespace ImageClassification;

//
// Singleton storing the configs needed to initialize an MediaPipe Tasks object and run inference.
// Controllers can observe the `InferenceConfigManager.notificationName` for any changes made by the user.
//
class InferenceConfigurationManager: NSObject
{
    Model model = DefaultConstants.model;
    public Model Model
    {
        get => model;
        set
        {
            model = value;
            PostConfigChangedNotification();
        }
    }

    float scoreThreshold = DefaultConstants.ScoreThreshold;
    public float ScoreThreshold
    {
        get => scoreThreshold;
        set
        {
            scoreThreshold = value;
            PostConfigChangedNotification();
        }
    }

    int maxResults = DefaultConstants.MaxResults;
    public int MaxResults
    {
        get => maxResults;
        set
        {
            maxResults = value;
            PostConfigChangedNotification();
            PostMaxResultChangedNotification();
        }
    }

    ImageClassifierDelegate _delegate = DefaultConstants.Delegate;
    public ImageClassifierDelegate Delegate
    {
        get => _delegate;
        set
        {
            _delegate = value;
            PostConfigChangedNotification();
        }
    }

    public static InferenceConfigurationManager SharedInstance = new();

    public static string NotificationName = NSNotification.FromName("com.google.mediapipe.inferenceConfigChanged", null).Name;
    public static string MaxResultChangeNotificationName = NSNotification.FromName("com.google.mediapipe.InferenceMaxResultsChanged", null).Name;
  
    private void PostConfigChangedNotification()
    {
        NSNotificationCenter.DefaultCenter.
            PostNotificationName(aName: InferenceConfigurationManager.NotificationName, anObject: null);
    }

    private void PostMaxResultChangedNotification()
    {
        NSNotificationCenter.DefaultCenter.
            PostNotificationName(aName: InferenceConfigurationManager.MaxResultChangeNotificationName, anObject: null);
    }
}
