using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Android.Widget;
using OpenId.AppAuth;
using Uri = Android.Net.Uri;

namespace LlmInference;

[Activity(Name = "com.google.mediapipe.examples.llminference.OAuthCallbackActivity", Label = "@string/app_name", Theme = "@style/Theme.LLMInference")]
public class OAuthCallbackActivity : Activity
{
    private AuthorizationService authService;
    private readonly string Tag = typeof(OAuthCallbackActivity).Name;

    override protected void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        authService = new AuthorizationService(this);

        var data = Intent.Data;
        if (data != null)
        {
            // Manually extract the authorization code
            var authCode = data.GetQueryParameter("code");
            // val authState = data.GetQueryParameter("state");

            if (authCode != null)
            {
                // Retrieve the code verifier that was used in the initial request
                var codeVerifier = SecureStorage.GetCodeVerifier(ApplicationContext);

                // Create a Token Request manually
                var tokenRequest = new TokenRequest.Builder(
                    AuthConfig.authServiceConfig, // Ensure this is properly set up
                    AuthConfig.clientId
                )
                    .SetGrantType(GrantTypeValues.AuthorizationCode)    
                    .SetAuthorizationCode(authCode)
                    .SetRedirectUri(Uri.Parse(AuthConfig.redirectUri))
                    .SetCodeVerifier(codeVerifier) // Required for PKCE
                    .Build();

                authService.PerformTokenRequest(tokenRequest, (response, ex) =>
                {
                    if (response != null) 
                    {
                        var accessToken = response.AccessToken;
                        SecureStorage.SaveToken(
                            ApplicationContext,
                            accessToken ?? ""
                        );
                        Toast.MakeText(this, "Sign in succeeded", ToastLength.Long).Show();

                        var licenseUrl = InferenceModel.Model.LicenseUrl;
                        if (string.IsNullOrEmpty(licenseUrl))
                        {
                            var intent = new Intent(this, typeof(MainActivity));
                            intent.PutExtra("NAVIGATE_TO", MainActivity.LOAD_SCREEN);
                            StartActivity(intent);
                        }
                        else
                        {
                            var intent = new Intent(this, typeof(LicenseAcknowledgmentActivity));
                            StartActivity(intent);
                        }
                    }
                    else
                    {
                        Log.Error(Tag, "OAuth Error: " + (ex?.Message ?? "unknown error"));
                    }
                    Finish();
                });
            }
            else
            {
                Log.Error(Tag, "No Authorization Code Found");
                Finish();
            }
        }
        else
        {
            Log.Error(Tag, "OAuth Failed: No Data in Intent");
            Finish();
        }
    }

    override protected void OnDestroy()
    {
        base.OnDestroy();

        authService?.Dispose();
    }
}
