using AndroidX.AppCompat.App;
using AndroidX.Navigation.Fragment;
using AndroidX.Navigation.UI;
using Google.Android.Material.BottomNavigation;

namespace AudioClassification;

[Activity(Name = "com.google.mediapipe.examples.audioclassification.MainActivity", Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
public class MainActivity : AppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        var navView = FindViewById<BottomNavigationView>(Resource.Id.navigation);
        var navHostFragment =
            SupportFragmentManager.FindFragmentById(Resource.Id.fragment_container) as NavHostFragment;
        var navController = navHostFragment.NavController;
        NavigationUI.SetupWithNavController(navView, navController);
    }

    public override void OnBackPressed()
    {
        Finish();
    }
}
