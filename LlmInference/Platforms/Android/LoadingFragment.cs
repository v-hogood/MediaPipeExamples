using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Google.Android.Material.ProgressIndicator;
using Java.IO;
using Square.OkHttp3;
using Xamarin.KotlinX.Coroutines;
using static AndroidX.Lifecycle.LifecycleOwnerKt;
using static LlmInference.BuildersKtx;
using Button = Android.Widget.Button;
using Exception = Java.Lang.Exception;
using File = Java.IO.File;
using Fragment = AndroidX.Fragment.App.Fragment;
using View = Android.Views.View;

namespace LlmInference;

public class LoadingFragment : Fragment,
    View.IOnClickListener
{
    private class MissingAccessTokenException : Exception
    {
        public MissingAccessTokenException() :
            base("Please try again after sign in") { }
    }

    private class UnauthorizedAccessException : Exception
    {
        public UnauthorizedAccessException() :
            base("Access denied. Please try again and grant the necessary permissions.") { }
    }

    private class MissingUrlException : Exception
    {
        public MissingUrlException(string message) :
            base(message) { }
    }

    private const int UnauthorizedCode = 401;

    public Action OnModelLoaded;
    public Action OnGoBack;

    private TextView loadingStatus;
    private CircularProgressIndicator progressIndicator;
    private Button cancelButton;
    private Button backButton;
    private IJob job;
    private OkHttpClient client = new();

    override public View OnCreateView(
        LayoutInflater inflater,
        ViewGroup container,
        Bundle savedInstanceState)
    {
        return inflater.Inflate(Resource.Layout.fragment_loading, container, false);
    }

    override public void OnViewCreated(View view, Bundle savedInstanceState)
    {
        base.OnViewCreated(view, savedInstanceState);

        loadingStatus = view.FindViewById<TextView>(Resource.Id.loading_status);
        progressIndicator = view.FindViewById<CircularProgressIndicator>(Resource.Id.progress_indicator);
        cancelButton = view.FindViewById<Button>(Resource.Id.btn_cancel);
        backButton = view.FindViewById<Button>(Resource.Id.btn_back);
        
        cancelButton.SetOnClickListener(this);
        
        backButton.SetOnClickListener(this);

        StartLoading();
    }

    public void OnClick(View v)
    {
        if (v == cancelButton)
        {
            job?.Cancel(null);
            GetLifecycleScope(this).Launch(Dispatchers.IO, () =>
            {
                DeleteDownloadedFile(RequireContext());
                ShowError("Download Cancelled");
            });
        }
        else if (v == backButton)
        {
            OnGoBack?.Invoke();
        }
    }

    private void StartLoading()
    {
        loadingStatus.Text = GetString(Resource.String.loading_model);
        progressIndicator.Indeterminate = true;
        cancelButton.Visibility = ViewStates.Gone;
        backButton.Visibility = ViewStates.Gone;

        job = GetLifecycleScope(this).Launch(Dispatchers.IO, () =>
        {
            var context = RequireContext().ApplicationContext;
            try
            {
                if (!InferenceModel.ModelExists(context))
                {
                    if (string.IsNullOrEmpty(InferenceModel.Model.Url))
                    {
                        throw new MissingUrlException("Please manually copy the model to " + InferenceModel.Model.Path);
                    }
                    
                    ShowDownloading();

                    DownloadModel(context, InferenceModel.Model, client,
                        (progress) =>
                    {
                        GetLifecycleScope(this).Launch(Dispatchers.Main, () =>
                        {
                            loadingStatus.Text = "Downloading Model: " + progress + "%";
                            progressIndicator.Progress = progress;
                        });
                    });
                }

                if (job.IsCancelled)
                    return;

                InferenceModel.ResetInstance(context);
                // Notify the UI that the model has finished loading
                WithContext(Dispatchers.Main, () =>
                {
                    OnModelLoaded?.Invoke();
                });
            }
            catch (MissingAccessTokenException e)
            {
                ShowError(e.LocalizedMessage ?? "Unknown Error");
            }
            catch (MissingUrlException e)
            {
                ShowError(e.LocalizedMessage ?? "Unknown Error");
            }
            catch (UnauthorizedAccessException e)
            {
                ShowError(e.LocalizedMessage ?? "Unknown Error");
            }
            catch (ModelSessionCreateFailException e)
            {
                ShowError(e.LocalizedMessage ?? "Unknown Error");
            }
            catch (ModelLoadFailException e)
            {
                ShowError(e.LocalizedMessage ?? "Unknown Error");
                // Remove invalid model file
                GetLifecycleScope(this).Launch(Dispatchers.Main, () =>
                {
                    DeleteDownloadedFile(context);
                });
            }
            catch (Exception e)
            {
                var error = e.LocalizedMessage ?? "Unknown Error";
                ShowError(error + 
                    " please manually copy the model to " + InferenceModel.Model.Path);
            }
        });
    }

    private void ShowDownloading()
    {
        WithContext(Dispatchers.Main, () =>
        {
            loadingStatus.Text = "Downloading Model: 0%";
            progressIndicator.Indeterminate = false;
            cancelButton.Visibility = ViewStates.Visible;
        });
    }

    private void ShowError(string message)
    {
        WithContext(Dispatchers.Main, () =>
        {
            loadingStatus.Text = message;
            progressIndicator.Visibility = ViewStates.Gone;
            cancelButton.Visibility = ViewStates.Gone;
            backButton.Visibility = ViewStates.Visible;
        });
    }

    private void DownloadModel(
        Context context,
        Model model,
        OkHttpClient client,
        Action<int> onProgressUpdate)
    {
        var requestBuilder = new Request.Builder().Url(model.Url);

        if (model.NeedsAuth)
        {
            var accessToken = SecureStorage.GetToken(context);
            if (string.IsNullOrEmpty(accessToken))
            {
                // Trigger LoginActivity if no access token is found
                Intent intent = new Intent(context, typeof(LoginActivity));
                intent.SetFlags(ActivityFlags.NewTask);
                context.StartActivity(intent);

                throw new MissingAccessTokenException();
            }
            else
            {
                requestBuilder.AddHeader("Authorization", "Bearer " + accessToken);
            }
        }

        var outputFile = new File(InferenceModel.ModelPathFromUrl(context));
        var response = client.NewCall(requestBuilder.Build()).Execute();
        if (!response.IsSuccessful)
        {
            if (response.Code() == UnauthorizedCode)
            {
                var accessToken = SecureStorage.GetToken(context);
                if (!string.IsNullOrEmpty(accessToken))
                {
                    // Remove invalid or expired token
                    SecureStorage.RemoveToken(context);
                }
                throw new UnauthorizedAccessException();
            }
            throw new Exception("Download failed: " + response.Code());
        }

        using (var inputStream = response.Body().ByteStream())
        {           
            using (var outputStream = new FileOutputStream(outputFile))
            {
                var buffer = new byte[4096];
                int bytesRead;
                long totalBytesRead = 0;
                long contentLength = response.Body()?.ContentLength() ?? -1;

                while ((bytesRead = inputStream.Read(buffer)) > 0)
                {
                    YieldKt.Yield(new Continuation()); // Check for cancellation
                    if (job.IsCancelled)
                        break;
                    outputStream.Write(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                    var progress = (contentLength > 0) ?
                        (int) (totalBytesRead * 100 / contentLength) : -1;
                    onProgressUpdate(progress);
                }
                outputStream.Flush();
            }
        }
    }

    private void DeleteDownloadedFile(Context context)
    {
        WithContext(Dispatchers.IO, () =>
        {
            var outputFile = new File(InferenceModel.ModelPathFromUrl(context));
            if (outputFile.Exists())
            {
                outputFile.Delete();
            }
        });
    }
}
