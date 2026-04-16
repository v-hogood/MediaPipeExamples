using Android.App;
using Android.OS;
using AndroidX.AppCompat.App;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace LlmInference;

[Activity(Name = "com.google.mediapipe.examples.llminference.MainActivity", Label = "@string/app_name", Theme = "@style/Theme.LLMInference", MainLauncher = true)]
public class MainActivity : AppCompatActivity
{
    public const string START_SCREEN = "start_screen";
    public const string LOAD_SCREEN = "load_screen";
    public const string CHAT_SCREEN = "chat_screen";

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Set our view from the "main" layout resource
        SetContentView(Resource.Layout.activity_main);

        var startDestination = Intent.GetStringExtra("NAVIGATE_TO") ?? START_SCREEN;
        NavigateTo(startDestination);
    }

    private void NavigateTo(string destination)
    {
        Fragment fragment = new SelectionFragment();
        
        if (destination.Equals(START_SCREEN))
        {
            fragment = new SelectionFragment()
                { OnModelSelected = new(() => NavigateTo(LOAD_SCREEN)) };
        }
        else if (destination.Equals(LOAD_SCREEN))
        {
            fragment = new LoadingFragment()
                { OnModelLoaded = new(() => NavigateTo(CHAT_SCREEN)),
                  OnGoBack = new(() => NavigateTo(START_SCREEN)) };
        }
        else if (destination.Equals(CHAT_SCREEN))
        {
            fragment = new ChatFragment()
                { OnClose = new(() => NavigateTo(START_SCREEN)) };
        };

        SupportFragmentManager.BeginTransaction()
            .Replace(Resource.Id.fragment_container, fragment)
            .Commit();
    }
}
