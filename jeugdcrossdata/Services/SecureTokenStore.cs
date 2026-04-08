using System.Security.Cryptography;
using System.Text;

namespace jeugdcrossdata.Services;

public sealed class SecureTokenStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("jeugdcrossdata-pat-v1");

    public string Encrypt(string pat)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(pat);
        var encryptedBytes = ProtectedData.Protect(plaintextBytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    public string Decrypt(string encryptedPat)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedPat);
        var plaintextBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plaintextBytes);
    }
}

