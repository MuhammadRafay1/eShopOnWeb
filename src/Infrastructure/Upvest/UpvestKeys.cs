using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Loads the EC private key used to sign Upvest requests. Handles the three
/// formats Upvest key setup can produce: plain SEC1/PKCS#8, encrypted PKCS#8
/// (<c>BEGIN ENCRYPTED PRIVATE KEY</c>), and the legacy OpenSSL encrypted SEC1
/// form that <see cref="ECDsa.ImportFromEncryptedPem"/> rejects.
/// </summary>
public static class UpvestKeys
{
    public static ECDsa LoadEcPrivateKey(string pem, string? passphrase)
    {
        var ec = ECDsa.Create();
        if (!pem.Contains("Proc-Type: 4,ENCRYPTED", StringComparison.Ordinal))
        {
            if (pem.Contains("BEGIN ENCRYPTED PRIVATE KEY"))
                ec.ImportFromEncryptedPem(pem, passphrase);     // PKCS#8, encrypted
            else
                ec.ImportFromPem(pem);                          // SEC1 or PKCS#8, plain
            return ec;
        }

        // Legacy OpenSSL encrypted SEC1 ("openssl ec -aes256").
        var lines = pem.Split('\n').Select(l => l.Trim()).ToList();
        var dek = lines.First(l => l.StartsWith("DEK-Info:"))["DEK-Info:".Length..].Trim().Split(',');
        if (dek[0] != "AES-256-CBC") throw new NotSupportedException("Unsupported DEK-Info: " + dek[0]);
        byte[] iv = Convert.FromHexString(dek[1]);
        byte[] data = Convert.FromBase64String(string.Concat(
            lines.Where(l => l.Length > 0 && !l.StartsWith("-----") && !l.Contains(':'))));
        byte[] pw = Encoding.UTF8.GetBytes(passphrase ?? ""), salt = iv[..8];
        byte[] d1 = MD5.HashData([.. pw, .. salt]);                 // OpenSSL EVP_BytesToKey, MD5
        byte[] d2 = MD5.HashData([.. d1, .. pw, .. salt]);
        using var aes = Aes.Create();
        aes.Key = [.. d1, .. d2];
        ec.ImportECPrivateKey(aes.DecryptCbc(data, iv), out _);
        return ec;
    }
}
