using System.Text;
using Foundation;
using Security;

namespace LlmInference;

// Encapsulates reading, writing and deleting from Keychain.
public struct KeychainHelper
{
    // Saves the value to the key.
    public static bool Save(string key, string value)
    {
        var data = NSData.FromString(value, NSStringEncoding.UTF8);
        if (data == null)
            return false;

        Delete(key: key);

        var query = new SecRecord(SecKind.GenericPassword)
        {
            Account = key,
            ValueData = data,
            Accessible = SecAccessible.WhenUnlockedThisDeviceOnly
        };

        return SecKeyChain.Add(query) == SecStatusCode.Success;
    }

    // Returns the value of the key if present.
    public static string Load(string key)
    {
        var query = new SecRecord(SecKind.GenericPassword)
        {
            Account = key
        };

        SecStatusCode status;
        var result = SecKeyChain.QueryAsData(query, false, out status);

        var data = result?.ToArray();
        if (data == null || data.Length == 0)
            return null;
  
        var value = Encoding.UTF8.GetString(data);

        return value;
    }

    // Deletes a key.
    public static bool Delete(string key)
    {
        var query = new SecRecord(SecKind.GenericPassword)
        {
            Account = key,
        };

        var status = SecKeyChain.Remove(query);
      
        return status == SecStatusCode.Success || status == SecStatusCode.ItemNotFound;
    }
  
    public static void Clear(string[] keys)
    {
        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key))
                continue;
            
            Delete(key: key);
        }
    }
}
