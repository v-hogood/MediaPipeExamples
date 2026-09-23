using Foundation;

namespace LlmInference;

public class AcknowledgeLicenseViewModel
{
    public NSUrl Url;
    string licenseAcknowledgedKey;

    bool disableContinue = true;
    public bool DisableContinue
    {   get => disableContinue;
        set
        {
            disableContinue = value;
            OnDisableContinueChanged?.Invoke(disableContinue);
        }
    }

    public Action<bool> OnDisableContinueChanged;
  
    public AcknowledgeLicenseViewModel(NSUrl url, string licenseAcknowledgedKey)
    {
        this.Url = url;
        this.licenseAcknowledgedKey = licenseAcknowledgedKey;
        DisableContinue = KeychainHelper.Load(licenseAcknowledgedKey) == null;
    }

    public void HandleLicenseViewed()
    {
        if (string.IsNullOrEmpty(licenseAcknowledgedKey))
          return;

        _ = KeychainHelper.Save(key: licenseAcknowledgedKey, value: "viewed");
        DisableContinue = false;
    }
}
