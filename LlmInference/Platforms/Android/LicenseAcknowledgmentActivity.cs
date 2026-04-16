using Android.App;
using Android.OS;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Browser.CustomTabs;
using Button = Android.Widget.Button;
using Intent = Android.Content.Intent;
using Uri = Android.Net.Uri;
using View = Android.Views.View;

namespace LlmInference;

[Activity(Name = "com.google.mediapipe.examples.llminference.LicenseAcknowledgmentActivity", Label = "@string/app_name", Theme = "@style/Theme.LLMInference")]
public class LicenseAcknowledgmentActivity : AppCompatActivity,
    View.IOnClickListener
{
    private Button acknowledgeButton;
    private Button continueButton;

    string licenseUrl;

    override protected void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_license_acknowledgment);

        licenseUrl = InferenceModel.Model.LicenseUrl;
        if (string.IsNullOrEmpty(licenseUrl))
        {
            Toast.MakeText(this, "Missing license URL, please try again", ToastLength.Long).Show();
            StartActivity(new Intent(this, typeof(MainActivity)));
            Finish();
        }

        acknowledgeButton = FindViewById<Button>(Resource.Id.btnAcknowledge);
        continueButton = FindViewById<Button>(Resource.Id.btnContinue);

        // Disable "Continue" button initially
        continueButton.Enabled = false;

        acknowledgeButton.SetOnClickListener(this);
    
        continueButton.SetOnClickListener(this);
    }

    void View.IOnClickListener.OnClick(View v)
    {
        if (v.Id == Resource.Id.btnAcknowledge)
        {
            var customTabsIntent = new CustomTabsIntent.Builder().Build();
            customTabsIntent.LaunchUrl(this, Uri.Parse(licenseUrl));

            // Enable "Continue" if user viewed license
            continueButton.Enabled = true;
        }
        else if (v.Id == Resource.Id.btnContinue)
        {
            var intent = new Intent(this, typeof(MainActivity));
            intent.PutExtra("NAVIGATE_TO", MainActivity.LOAD_SCREEN);
            StartActivity(intent);
            Finish();
        };
    }
    override protected void OnResume()
    {
        base.OnResume();
        // Enable "Continue" if user viewed license
        // continueButton.setEnabled(true);
    }
}
