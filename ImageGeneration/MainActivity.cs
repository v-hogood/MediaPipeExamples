using Android.Content;
using Android.Views;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;

namespace ImageGeneration;

[Activity(Name = "com.google.mediapipe.examples.imagegeneration.MainActivity", Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
public class MainActivity : AppCompatActivity,
    View.IOnClickListener
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        FindViewById<AppCompatButton>(Resource.Id.btnDiffusion).SetOnClickListener(this);

        FindViewById<AppCompatButton>(Resource.Id.btnPlugins).SetOnClickListener(this);

        FindViewById<AppCompatButton>(Resource.Id.btnLoRA).SetOnClickListener(this);
    }

    void View.IOnClickListener.OnClick(View v)
    {
        if (v.Id == Resource.Id.btnDiffusion)
            StartActivity(new Intent(this, typeof(DiffusionActivity)));

        if (v.Id == Resource.Id.btnPlugins)
            StartActivity(new Intent(this, typeof(PluginActivity)));

        if (v.Id == Resource.Id.btnLoRA)
            StartActivity(new Intent(this, typeof(LoRAWeightActivity)));
    }
}
