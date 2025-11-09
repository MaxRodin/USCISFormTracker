using System.Security.Cryptography;
using System.Text;

namespace USCISFormTracker.Core;

public class Sha256Hasher : IHasher
{
    public string ComputeHash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
