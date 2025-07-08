using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.Widget;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.BottomSheet;
using Google.Android.Material.Button;
using Google.Android.Material.TextField;
using MediaPipe.Tasks.Text.TextClassifier;
using View = Android.Views.View;

namespace TextClassification
{
    [Activity(Name = "com.google.mediapipe.examples.textclassifier.MainActivity", Label = "@string/app_name", Theme = "@style/AppTheme.TextClassifier", MainLauncher = true)]
    public class MainActivity : AppCompatActivity,
        View.IOnClickListener,
        TextClassifierHelper.TextResultsListener,
        RadioGroup.IOnCheckedChangeListener
    {
        private const string Tag = "TextClassifier";

        TextClassifierHelper classifierHelper;
        private ResultsAdapter adapter = new ResultsAdapter();

        public void OnResult(
            TextClassifierResult results,
            long inferenceTime)
        {
            RunOnUiThread(() =>
            {
                FindViewById<TextView>(Resource.Id.inference_time_val).Text =
                    inferenceTime + " ms";

                adapter.UpdateResult(results.ClassificationResult()
                    .Classifications().First()
                    .Categories().OrderByDescending(it =>
                        it.Score()).ToList(),
                        classifierHelper.CurrentModel);
            });
        }

        public void OnError(string error)
        {
            Toast.MakeText(this, error, ToastLength.Short).Show();
        }

        public void OnClick(View v)
        {
            var text = FindViewById<TextInputEditText>(Resource.Id.input_text).Text;

            if (text == null || text.Length == 0)
            {
                classifierHelper.Classify(Resources.GetString(Resource.String.default_edit_text));
            }
            else
            {
                classifierHelper.Classify(text);
            }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            // Create the classification helper that will do the heavy lifting
            classifierHelper = new TextClassifierHelper(
                this, this);

            // Classify the text in the TextEdit box (or the default if nothing is added)
            // on button click.
            FindViewById<MaterialButton>(Resource.Id.classify_btn).SetOnClickListener(this);

            FindViewById<RecyclerView>(Resource.Id.results).SetAdapter(adapter);

            InitBottomSheetControls();
        }

        private void InitBottomSheetControls()
        {
            var behavior = BottomSheetBehavior.From(FindViewById<NestedScrollView>(Resource.Id.bottom_sheet_layout));
            behavior.State = BottomSheetBehavior.StateExpanded;

            // Allows the user to switch between the classification models that are available.
            FindViewById<RadioGroup>(Resource.Id.model_selector).SetOnCheckedChangeListener(this);
        }

        public void OnCheckedChanged(RadioGroup group, int checkedId)
        {
            switch(checkedId)
            {
                case Resource.Id.wordvec:
                    classifierHelper.CurrentModel = TextClassifierHelper.WordVec;
                    classifierHelper.InitClassifier();
                    break;
                case Resource.Id.mobilebert:
                    classifierHelper.CurrentModel = TextClassifierHelper.MobileBert;
                    classifierHelper.InitClassifier();
                    break;
            };
        }

        public override void OnBackPressed()
        {
            if (Build.VERSION.SdkInt == BuildVersionCodes.Q)
            {
                // Workaround for Android Q memory leak issue in IRequestFinishCallback$Stub.
                // (https://issuetracker.google.com/issues/139738913)
                FinishAfterTransition();
            }
            else
            {
#pragma warning disable CA1422
                base.OnBackPressed();
#pragma warning restore CA1422
            }
        }
    }
}
