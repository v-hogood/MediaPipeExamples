using Android.Content;
using Android.OS;
using Android.Util;
using Java.Lang;
using Java.Util.Concurrent;
using MediaPipe.Tasks.Core;
using MediaPipe.Tasks.Text.TextClassifier;

namespace TextClassification
{
    public class TextClassifierHelper
    {
        public string CurrentModel = WordVec;
        private Context context;
        private TextResultsListener listener;

        private TextClassifier textClassifier;

        private ScheduledThreadPoolExecutor executor;

        public TextClassifierHelper(Context context, TextResultsListener listener)
        {
            this.context = context;
            this.listener = listener;

            InitClassifier();
        }

        public void InitClassifier()
        {
            var baseOptionsBuilder = BaseOptions.InvokeBuilder().SetModelAssetPath(CurrentModel);

            try
            {
                var baseOptions = baseOptionsBuilder.Build();
                var optionsBuilder = TextClassifier.TextClassifierOptions.InvokeBuilder()
                    .SetBaseOptions(baseOptions);
                var options = optionsBuilder.Build();
                textClassifier = TextClassifier.CreateFromOptions(context, options);
            }
            catch (IllegalStateException e)
            {
                listener.OnError(
                    "Text classifier failed to initialize. See error logs for details");
                Log.Error(Tag,
                    "Text classifier failed to load the task with error: " + e.Message);
            }
        }

        public void Classify(string text)
        {
            executor = new ScheduledThreadPoolExecutor(1);

            executor.Execute(new Runnable(() =>
            {
                // inferenceTime is the amount of time, in milliseconds, that it takes to
                // classify the input text.
                var inferenceTime = SystemClock.UptimeMillis();

                var results = textClassifier.Classify(text);

                inferenceTime = SystemClock.UptimeMillis() - inferenceTime;

                listener.OnResult(results, inferenceTime);
            }));
        }

        public interface TextResultsListener
        {
            void OnError(string error);
            void OnResult(TextClassifierResult results, long inferenceTime);
        }

        private const string Tag = "TextClassifierHelper";

        public const string WordVec = "wordvec.tflite";
        public const string MobileBert = "mobilebert.tflite";
    }
}
