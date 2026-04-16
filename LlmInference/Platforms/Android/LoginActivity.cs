using Android.App;
using Android.OS;
using Android.Util;
using ImageButton = Android.Widget.ImageButton;
using AndroidX.AppCompat.App;
using Java.Security;
using OpenId.AppAuth;
using Uri = Android.Net.Uri;
using View = Android.Views.View;

namespace LlmInference;

[Activity(Name = "com.google.mediapipe.examples.llminference.LoginActivity", Label = "@string/app_name", Theme = "@style/Theme.LLMInference")]
public class LoginActivity : AppCompatActivity,
    View.IOnClickListener
{
    private AuthorizationService authService;
    private string codeVerifier;
    private string codeChallenge;

    override protected void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_login);

        authService = new AuthorizationService(this);

        var loginButton = FindViewById<ImageButton>(Resource.Id.btnLogin);
        loginButton.SetOnClickListener(this);
    }

    public void OnClick(View v)
    {
        LoginWithHuggingFace(); // Start OAuth login when button is clicked
    }

    private void LoginWithHuggingFace()
    {
        // Generate PKCE parameters
        codeVerifier = GenerateCodeVerifier();
        codeChallenge = GenerateCodeChallenge(codeVerifier);

        // Save the code verifier securely for later use in token exchange
        SecureStorage.SaveCodeVerifier(ApplicationContext, codeVerifier);

        var authRequest = new AuthorizationRequest.Builder(
            AuthConfig.authServiceConfig,
            AuthConfig.clientId,
            ResponseTypeValues.Code,
            Uri.Parse(AuthConfig.redirectUri)
        ).SetScope("read-repos") // Adjust scopes if needed
            .SetCodeVerifier(codeVerifier, codeChallenge, "S256") // Include PKCE
            .Build();

        var authIntent = authService.GetAuthorizationRequestIntent(authRequest);
        StartActivity(authIntent); // Launch OAuth login page
    }

    private string GenerateCodeVerifier()
    {
        byte[] random = new byte[32];
        new SecureRandom().NextBytes(random);
        return Base64.EncodeToString(random, Base64Flags.UrlSafe | Base64Flags.NoPadding | Base64Flags.NoWrap);
    }

    private string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(codeVerifier);
        var digest = MessageDigest.GetInstance("SHA-256").Digest(bytes);
        return Base64.EncodeToString(digest, Base64Flags.UrlSafe | Base64Flags.NoPadding | Base64Flags.NoWrap);
    }

    override protected void OnDestroy()
    {
        base.OnDestroy();
        
        authService?.Dispose();
    }
}
