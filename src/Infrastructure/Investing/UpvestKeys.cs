using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Loads the EC private key Upvest requests are signed with. Handles the three PEM shapes Upvest
/// key setup can produce: plain SEC1/PKCS#8, encrypted PKCS#8, and the legacy OpenSSL encrypted SEC1
/// form that <see cref="ECDsa.ImportFromEncryptedPem(ReadOnlySpan{char}, ReadOnlySpan{char})"/> rejects.
/// </summary>
public static class UpvestKeys
{
    public static ECDsa LoadEcPrivateKey(string pem, string? passphrase)
    {
        var ec = ECDsa.Create();
        if (!pem.Contains("Proc-Type: 4,ENCRYPTED", StringComparison.Ordinal))
        {
            if (pem.Contains("BEGIN ENCRYPTED PRIVATE KEY"))
            {
                ec.ImportFromEncryptedPem(pem, passphrase);   // PKCS#8, encrypted
            }
            else
            {
                ec.ImportFromPem(pem);                          // SEC1 or PKCS#8, plain
            }

            return ec;
        }

        // Legacy OpenSSL encrypted SEC1 (`openssl ec -aes256`), which ImportFromEncryptedPem rejects.
        var lines = pem.Split('\n').Select(l => l.Trim()).ToList();
        var dek = lines.First(l => l.StartsWith("DEK-Info:"))["DEK-Info:".Length..].Trim().Split(',');
        if (dek[0] != "AES-256-CBC") throw new NotSupportedException("DEK-Info " + dek[0]);
        byte[] iv = Convert.FromHexString(dek[1]);
        byte[] data = Convert.FromBase64String(string.Concat(
            lines.Where(l => l.Length > 0 && !l.StartsWith("-----") && !l.Contains(':'))));
        byte[] pw = Encoding.UTF8.GetBytes(passphrase ?? string.Empty), salt = iv[..8];
        byte[] d1 = MD5.HashData([.. pw, .. salt]), d2 = MD5.HashData([.. d1, .. pw, .. salt]);
        using var aes = Aes.Create();
        aes.Key = [.. d1, .. d2];
        ec.ImportECPrivateKey(aes.DecryptCbc(data, iv), out _);
        return ec;
    }
}
