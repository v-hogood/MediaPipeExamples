using Android.Content;
using AndroidX.Security.Crypto;

namespace LlmInference;

public static class SecureStorage
{
    private const string PrefsName = "secure_prefs";
    private const string KeyAccessToken = "access_token";
    private const string KeyCodeVerifier = "code_verifier";

    public static void SaveCodeVerifier(Context context, string codeVerifier) 
    {
        var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
        prefs.Edit().PutString(KeyCodeVerifier, codeVerifier).Apply();
    }

    public static string GetCodeVerifier(Context context)
    {
        var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
        return prefs.GetString(KeyCodeVerifier, null);
    }

    public static void SaveToken(Context context, string token)
    {
        var masterKeyAlias = MasterKeys.GetOrCreate(MasterKeys.Aes256GcmSpec);
        var sharedPreferences = EncryptedSharedPreferences.Create(
            PrefsName,
            masterKeyAlias,
            context,
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.Aes256Siv,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.Aes256Gcm
        );
        sharedPreferences.Edit().PutString(KeyAccessToken, token).Apply();
    }

    public static string GetToken(Context context)
    {
        var masterKeyAlias = MasterKeys.GetOrCreate(MasterKeys.Aes256GcmSpec);
        var sharedPreferences = EncryptedSharedPreferences.Create(
            PrefsName,
            masterKeyAlias,
            context,
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.Aes256Siv,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.Aes256Gcm
        );
        return sharedPreferences.GetString(KeyAccessToken, null);
    }

    public static void RemoveToken(Context context)
    {
        var masterKeyAlias = MasterKeys.GetOrCreate(MasterKeys.Aes256GcmSpec);
        var sharedPreferences = EncryptedSharedPreferences.Create(
            PrefsName,
            masterKeyAlias,
            context,
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.Aes256Siv,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.Aes256Gcm
        );
        sharedPreferences.Edit().Remove(KeyAccessToken).Apply();
    }
}
