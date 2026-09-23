using System.Security.Cryptography;
using System.Text;
using Foundation;

namespace LlmInference;

public class OAuthService : NSObject
{
    AuthConfig config;
    private string currentState;

    public OAuthService(AuthConfig config)
    {
        this.config = config;
    }

    public NSUrl BuildAuthorizationUrl()
    {
        // Generate PKCE challenge and state
        var (verifier, challenge) = GeneratePkce();

        // Store code verifier securely
        if (!KeychainHelper.Save(AuthConfig.CodeVerifierKeychainKey, verifier))
        {
            throw new OAuthException(OAuthError.InternalError, "Could not save code verifier to keychain.");
        }

        var state = GenerateState();

        // Construct Authorization URL
        var scopeString = string.Join(" ", AuthConfig.DefaultScopes);
        using var components = new NSUrlComponents(url: new NSUrl(AuthConfig.AuthEndpoint), resolveAgainstBaseUrl: false)
        {   
            QueryItems = new[]
            {
                new NSUrlQueryItem("client_id", AuthConfig.clientId),
                new NSUrlQueryItem("redirect_uri", AuthConfig.redirectUri),
                new NSUrlQueryItem("response_type", "code"),
                new NSUrlQueryItem("scope", scopeString),
                new NSUrlQueryItem("code_challenge", challenge),
                new NSUrlQueryItem("code_challenge_method", "S256"),
                new NSUrlQueryItem("state", state) // Include state
            }
        };

        var authUrl = components?.Url;
        if (authUrl == null)
        {
            throw new OAuthException(OAuthError.InternalError, "Could not create authorization URL.");
        }
        
        // Store state for validation
        currentState = state;

        return authUrl;
    }

    // Retrieves the current access token from the keychain.
    public string GetAccessToken()
    {
        var accessToken = KeychainHelper.Load(key: AuthConfig.AccessTokenKeychainKey);
        if (accessToken == null)
        {
            throw new OAuthException(OAuthError.InternalError, "Access token not found.");
        }

        return accessToken;
    }

    // Checks if an access token exists in the keychain.
    public bool HasAccessToken()
    {
        return KeychainHelper.Load(key: AuthConfig.AccessTokenKeychainKey) != null;
    }

    // Clears the stored access token from the keychain.
    public void ClearAccessToken()
    {
        if (!KeychainHelper.Delete(key: AuthConfig.AccessTokenKeychainKey))
        {
            throw new OAuthException(OAuthError.InternalError, "Unexpected error clearing access token.");
        }
    }

    // Handles the callback to the app from the OAuth authorization endpoint.
    // Validates that a code and state is present in the callback URL and checks for any state mismatch.
    // Exchanges the code for the  access token.
    public async Task HandleCallbackAsync(NSUrl callbackUrl)
    {
        try
        {
            if (callbackUrl == null)
            {
                throw new OAuthException(OAuthError.InvalidCallbackUrl, "Authentication callback URL was missing.");
            }

            using var components = new NSUrlComponents(url: callbackUrl, resolveAgainstBaseUrl: false);
            var queryItems = components?.QueryItems;
            if (queryItems == null)
            {
                throw new OAuthException(OAuthError.InternalError, "Could not parse callback URL components.");
            }

            // Extract code and state
            var code = queryItems.FirstOrDefault(item => item.Name == "code")?.Value;
            var returnedState = queryItems.FirstOrDefault(item => item.Name == "state")?.Value;

            // Validate state (CSRF protection)
            if (string.IsNullOrEmpty(returnedState))
            {
                throw new OAuthException(OAuthError.InvalidCallbackUrl, "Authentication callback URL missing 'state' parameter.");
            }

            if (string.IsNullOrEmpty(currentState) || returnedState != currentState)
            {
                throw new OAuthException(OAuthError.InvalidCallbackUrl, "Authentication callback state parameter did not match.");
            }

            if (string.IsNullOrEmpty(code))
            {
                throw new OAuthException(OAuthError.InvalidCallbackUrl, "Authentication callback URL missing 'code' parameter.");
            }

            await ExchangeCodeForTokenAsync(code).ConfigureAwait(false);
        }
        finally
        {
            CleanupStateAndVerifier();
        }
    }

    // Exchanges the code for access token by making a post request to the token endpoint.
    private async Task ExchangeCodeForTokenAsync(string code)
    {
        var codeVerifier = KeychainHelper.Load(key: AuthConfig.CodeVerifierKeychainKey);
        if (codeVerifier == null)
        {
            throw new OAuthException(OAuthError.InternalError, "Could not retrieve code verifier for token exchange.");
        }

        var postString = string.Join("&", new[]
        {
            $"grant_type=authorization_code",
            $"code={System.Uri.EscapeDataString(code)}",
            $"redirect_uri={System.Uri.EscapeDataString(AuthConfig.redirectUri)}",
            $"client_id={System.Uri.EscapeDataString(AuthConfig.clientId)}",
            $"code_verifier={System.Uri.EscapeDataString(codeVerifier)}"
        });

        using var postData = NSData.FromString(postString, NSStringEncoding.UTF8);
        if (postData == null)
        {
            throw new OAuthException(OAuthError.InternalError, "Failed to encode token request body.");
        }

        try
        {
            var accessTokenKey = "access_token";
            var headers = NSDictionary.FromObjectsAndKeys(
                new[] { new NSString("application/x-www-form-urlencoded") },
                new[] { new NSString("Content-Type") });

            var response = await NetworkService.Shared.PostRequestAsync(
                new NSUrl(AuthConfig.TokenEndpoint),
                postData,
                headers).ConfigureAwait(false);

            var accessToken = response[accessTokenKey] as NSString;
            if (accessToken == null)
            {
                throw new OAuthException(OAuthError.MissingAccessTokenInResponse, "Token response did not contain an access token.");
            }

            // Store the access token securely
            if (!KeychainHelper.Save(key: AuthConfig.AccessTokenKeychainKey, value: accessToken.ToString()))
            {
                throw new OAuthException(OAuthError.InternalError, "Could not save access token.");
            }
        }
        catch (NetworkService.NetworkException ex)
        {
            // Catches NetworkService errors or JSON parsing errors
            throw new OAuthException(OAuthError.TokenRequestFailed, ex);
        }
    }

    // Generates the verifier and challenge for the OAuth flow.
    private static (string verifier, string challenge) GeneratePkce()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);

        var verifier = Base64UrlEncode(bytes);

        var verifierData = Encoding.UTF8.GetBytes(verifier);
        if (verifierData == null)
        {
            throw new OAuthException(OAuthError.InternalError, "Failed to encode code verifier.");
        }

        var challengeBytes = SHA256.HashData(verifierData);
        var challenge = Base64UrlEncode(challengeBytes);

        return (verifier, challenge);
    }

    // Generates the state for verifying the CSRF protection.
    private static string GenerateState()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    // Cleans up the temporary state and code verifier from memory and keychain.
    private void CleanupStateAndVerifier()
    {
        currentState = null;

        if (!KeychainHelper.Delete(key:AuthConfig.CodeVerifierKeychainKey))
        {
            Console.WriteLine("OAuthService: Warning - Failed to delete code verifier from keychain.");
        }
    }

    public enum OAuthError
    {
        PkceGenerationFailed,
        InvalidCallbackUrl,
        TokenRequestFailed,
        MissingAccessTokenInResponse,
        InternalError
    }

    public class OAuthException : Exception
    {
        public OAuthError Error { get; }
        
        public string Context { get; }
        public Exception UnderlyingError { get; }

        // Constructors mapping to the different payload shapes (mimicking Swift enum cases)
        public OAuthException(OAuthError error)
        {
            Error = error;
        }

        public OAuthException(OAuthError error, string context)
        {
            Error = error;
            Context = context;
        }

        public OAuthException(OAuthError error, Exception underlyingError)
        {
            Error = error;
            UnderlyingError = underlyingError;
        }

        public string ErrorDescription => Error switch
        {
            OAuthError.PkceGenerationFailed => "Failed to generate PKCE challenge.",
            OAuthError.InvalidCallbackUrl => Context,
            OAuthError.TokenRequestFailed => "Request to exchange code for token failed.",
            OAuthError.MissingAccessTokenInResponse => "Token response did not contain an access token.",
            OAuthError.InternalError => "An internal error occurred: " + Context,
            _ => ""
        };
    }

    // MARK: - Data Helpers
    private static string Base64UrlEncode(byte[] data)
    {
        var encoded = Convert.ToBase64String(data)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);

        return encoded;
    }
}
