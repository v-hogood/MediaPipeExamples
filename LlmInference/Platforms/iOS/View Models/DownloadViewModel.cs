using AuthenticationServices;
using Foundation;

namespace LlmInference;

public class DownloadViewModel
{
    DownloadState state = new DownloadState.NotInitiated();
    public DownloadState State
    {
        get => state;
        set
        {
            state = value;
            OnStateChanged?.Invoke(value);
        }
    }

    double progress = 0.0;
    public double Progress
    {
        get => progress;
        set
        {
            progress = value;
            OnProgressChanged?.Invoke(value);
        }
    }

    public Action<DownloadState> OnStateChanged;
    public Action<double> OnProgressChanged;

    string modelName;
    public bool AuthRequired =>
      modelCategory.NeedsAuth && !oauthService.HasAccessToken();

    private OAuthService oauthService = new OAuthService(new AuthConfig());
    private Model modelCategory;

    private Task downloadTask;
    private CancellationTokenSource downloadCts;

    public string ModelName => modelName;

    public DownloadViewModel(Model modelCategory)
    {
        this.modelCategory = modelCategory;
        this.modelName = this.modelCategory.Name;
    }

    public void Download()
    {
        if (downloadTask != null) return;

        downloadCts = new CancellationTokenSource();
        var token = downloadCts.Token;

        downloadTask = Task.Run(async () =>
        {
            try
            {
                await PerformDownloadAsync(
                    downloadUrl: modelCategory.DownloadUrl, 
                    destinationUrl: modelCategory.DownloadDestination,
                    token
                );
            }
            catch (NetworkService.NetworkException networkException)
            {
                this.state = new DownloadState.Error(DownloadError.From(networkException));
                HandleNetworkError(networkException.Error);
            }
            catch (OperationCanceledException)
            {
                // Task was canceled cleanly, do not propagate as an error
            }
            catch (Exception ex)
            {
                this.state = new DownloadState.Error(DownloadError.Generic(ex));
            }
            finally
            {
                this.downloadCts?.Dispose();
                this.downloadCts = null;
                this.downloadTask = null;
            }
        }, token);
    }

    public void CancelDownload()
    {
        downloadCts?.Cancel();
        downloadCts?.Dispose();
        downloadCts = null;
        downloadTask = null;
        UpdateStatesOnCancellation();
    }

    bool HandleDownloadErrorDismissed()
    {
        if (state is not DownloadState.Error)
        {
          return false;
        }
        if (state.error.NetworkError.Error ==  NetworkService.NetworkError.Unauthorized ||
            state.error.NetworkError.Error == NetworkService.NetworkError.Forbidden)
             return true;
        else
            return false;
    }

    public NSUrl GetAuthorizationUrl()
    {
        try
        {
            return oauthService.BuildAuthorizationUrl();
        }
        catch (OAuthService.OAuthException exception)
        {
            state = new DownloadState.Error(DownloadError.From(oauthError: exception));
        }
        catch (Exception exception)
        {
            state = new DownloadState.Error(DownloadError.Generic(error: exception));
        }

        return null;
    }

    public async Task<bool> HandleAuthenticationCallback(NSUrl callbackURL)
    {
        try
        {
            await oauthService.HandleCallbackAsync(callbackURL);
        }
        catch (OAuthService.OAuthException exception)
        {
            state = new DownloadState.Error(DownloadError.From(oauthError: exception));
        }
        catch (Exception exception)
        {
            state = new DownloadState.Error(DownloadError.Generic(error: exception));
        }

        return false;
    }

    public void HandleWebAuthenticationError(NSError error)
    {
        // if (error is ASWebAuthenticationSessionError webAuthenticationError)
        // {
        //     State = new DownloadState.Error(DownloadError.From(webAuthenticationError.Code));
        // }
        // else
        {
            State = new DownloadState.Error(DownloadError.Generic(error: error));
        }
    }

    private async Task PerformDownloadAsync(NSUrl downloadUrl, NSUrl destinationUrl,
        CancellationToken token)
    {
        NSDictionary headers = null;

        if (modelCategory.NeedsAuth)
        {
            var accessToken = oauthService.GetAccessToken();
            headers = new NSDictionary("Authorization", "Bearer " + accessToken);
        }

        await foreach(var downloadEvent in NetworkService.Shared.DownloadFileAsync(
            from: downloadUrl, to: destinationUrl, headers: headers))
        {
            if (token.IsCancellationRequested)
            {
                UpdateStatesOnCancellation();
                return;
            }
            state = new DownloadState.Progress();
            if (downloadEvent is NetworkService.DownloadEvent.Progress)
            {
                progress = (downloadEvent as NetworkService.DownloadEvent.Progress).progress;
            }
            else if (downloadEvent is NetworkService.DownloadEvent.Completed)
            {
                progress = 100.0;
                state = new DownloadState.Completed();
            } 
        }
    }

    private void HandleNetworkError(NetworkService.NetworkError error)
    {
        if (error == NetworkService.NetworkError.Forbidden)
        {
            _ = KeychainHelper.Delete(key: modelCategory.LicenseAcknowledgedKey);
            oauthService.ClearAccessToken();
        }
        else if (error == NetworkService.NetworkError.Unauthorized)
        {
            oauthService.ClearAccessToken();
        }
    }

    private void UpdateStatesOnCancellation()
    {
        this.state = new DownloadState.NotInitiated();
        this.progress = 0.0;
    }

    // MARK: - Download State and Errors
    public record DownloadState
    {
        public record NotInitiated : DownloadState;
        public record LoginRequired : DownloadState;
        public record Progress : DownloadState;
        public record Completed : DownloadState;
        public record Error(DownloadError error) : DownloadState;

        public virtual bool Equals(DownloadState other) =>
             this.GetType() == other?.GetType();
        
        public override int GetHashCode() =>
            GetType().GetHashCode();

        public DownloadError error => this switch
        {
            Error err => err.error,
            _ => null
        };
    }

    public class DownloadError
    {
        DownloadError(string title, string description, NetworkService.NetworkException networkError = null)
        {
            this.ErrorDescription = title;
            this.FailureReason = description;
            this.NetworkError = networkError;
        }
        public string ErrorDescription;

        public string FailureReason;

        public NetworkService.NetworkException NetworkError;

        public static DownloadError From(NetworkService.NetworkException networkError)
        {
            switch (networkError.Error)
            {
                case NetworkService.NetworkError.Unauthorized:
                    return new DownloadError(
                        title: "Unauthorized Request",
                        description: """
                          The request could not be authorized.
                          Please click retry by clicking on the download button to refresh the access session.
                          """,
                        networkError: networkError
                    );
                case NetworkService.NetworkError.Forbidden:
                    return new DownloadError(
                        title: "Forbidden Request",
                        description: """
                          You may not have accepted the license agreement of the model on Hugging Face.
                          You will be redirected to the license acknowledgement screen when you click "OK".
                          """,
                        networkError: networkError
                    );
                default:
                    return new DownloadError(
                        title: networkError.ErrorDescription!,
                        description: networkError.FailureReason,
                        networkError: networkError
                    );
            }
        }

        public static DownloadError From(OAuthService.OAuthException oauthError)
        {
            return new DownloadError(
                title: "OAuth Error",
                description: oauthError.ErrorDescription
                    ?? "Some error occurred while processing the OAuth flow."
            );
        }

        public static DownloadError From(ASWebAuthenticationSessionErrorCode webAuthErrorCode)
        {
            switch (webAuthErrorCode)
            {
                case ASWebAuthenticationSessionErrorCode.CanceledLogin:
                    return new DownloadError(
                        title: "Authentication Canceled",
                        description: "The login was canceled by the user."
                    );
                default:
                    return new DownloadError(
                        title: "Authentication Error",
                        description: webAuthErrorCode.ToString() + " Check your network or try again later."
                    );
            }
        }

        public static DownloadError Generic(NSError error)
        {
            return new DownloadError(
                title: "Unexpected Error",
                description: error.LocalizedDescription
            );
        }

        public static DownloadError Generic(Exception error)
        {
            return new DownloadError(
                title: "Unexpected Exception",
                description: error.Message
            );
        }
    }
}
