#if __ANDROID__
using OpenId.AppAuth;
using Uri = Android.Net.Uri;
#elif __IOS__
using Uri = Foundation.NSUrl;
#endif

namespace LlmInference;

public struct AuthConfig
{
    // Replace these values with your actual OAuth credentials
    public const string clientId = "19943f22-042c-43f8-96bd-6522ffa8bdfe"; // Hugging Face Client ID
    public const string redirectUri = "com.google.mediapipe.examples.llminference://oauth2callback";

    // OAuth 2.0 Endpoints (Authorization + Token Exchange)
    public const string AuthEndpoint = "https://huggingface.co/oauth/authorize";
    public const string TokenEndpoint = "https://huggingface.co/oauth/token";
    public static readonly string[] DefaultScopes = new[] { "read-repos" };

#if __ANDROID__
    // OAuth service configuration (AppAuth library requires this)
    public static AuthorizationServiceConfiguration authServiceConfig = new AuthorizationServiceConfiguration(
        Uri.Parse(AuthEndpoint), // Authorization endpoint
        Uri.Parse(TokenEndpoint) // Token exchange endpoint
    );
#elif __IOS__
    // Keychain keys specific to this service
    public const string AccessTokenKeychainKey = "com.google.mediapipe.examples.llminference.huggingface.accessToken";
    public const string CodeVerifierKeychainKey = "com.google.mediapipe.examples.llminference.huggingface.codeVerifier";
#endif
}
