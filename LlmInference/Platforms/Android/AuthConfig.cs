namespace LlmInference;

using OpenId.AppAuth;
using Uri = Android.Net.Uri;

public static class AuthConfig
{
    // Replace these values with your actual OAuth credentials
    public const string clientId = "19943f22-042c-43f8-96bd-6522ffa8bdfe"; // Hugging Face Client ID
    public const string redirectUri = "com.google.mediapipe.examples.llminference://oauth2callback";

    // OAuth 2.0 Endpoints (Authorization + Token Exchange)
    private const string authEndpoint = "https://huggingface.co/oauth/authorize";
    private const string tokenEndpoint = "https://huggingface.co/oauth/token";

    // OAuth service configuration (AppAuth library requires this)
    public static AuthorizationServiceConfiguration authServiceConfig = new AuthorizationServiceConfiguration(
        Uri.Parse(authEndpoint), // Authorization endpoint
        Uri.Parse(tokenEndpoint) // Token exchange endpoint
    );
}
