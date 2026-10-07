using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Loads the EC private key used to sign Upvest requests. Handles the three PEM shapes Upvest's key
/// setup can produce: plain SEC1/PKCS#8, encrypted PKCS#8, and the legacy OpenSSL encrypted SEC1 block
/// that <see cref="ECDsa.ImportFromEncryptedPem(System.ReadOnlySpan{char}, System.ReadOnlySpan{char})"/>
/// rejects.
/// </summary>
public static class UpvestKeys
{
    public static ECDsa LoadEcPrivateKey(string pem, string? passphrase)
    {
        var ec = ECDsa.Create();
        if (!pem.Contains("Proc-Type: 4,ENCRYPTED", StringComparison.Ordinal))
        {
            if (pem.Contains("BEGIN ENCRYPTED PRIVATE KEY", StringComparison.Ordinal))
            {
                ec.ImportFromEncryptedPem(pem, passphrase);   // PKCS#8, encrypted
            }
            else
            {
                ec.ImportFromPem(pem);                        // SEC1 or PKCS#8, plain
            }

            return ec;
        }

        // SEC1 encrypted the legacy OpenSSL way (`openssl ec -aes256`), which ImportFromEncryptedPem rejects.
        var lines = pem.Split('\n').Select(l => l.Trim()).ToList();
        var dek = lines.First(l => l.StartsWith("DEK-Info:", StringComparison.Ordinal))["DEK-Info:".Length..].Trim().Split(',');
        if (dek[0] != "AES-256-CBC")
        {
            throw new NotSupportedException("Unsupported DEK-Info cipher: " + dek[0]);
        }

        byte[] iv = Convert.FromHexString(dek[1]);
        byte[] data = Convert.FromBase64String(string.Concat(
            lines.Where(l => l.Length > 0 && !l.StartsWith("-----", StringComparison.Ordinal) && !l.Contains(':'))));
        byte[] pw = Encoding.UTF8.GetBytes(passphrase ?? string.Empty);
        byte[] salt = iv[..8];
        byte[] d1 = MD5.HashData([.. pw, .. salt]);
        byte[] d2 = MD5.HashData([.. d1, .. pw, .. salt]);   // OpenSSL EVP_BytesToKey, MD5
        using var aes = Aes.Create();
        aes.Key = [.. d1, .. d2];
        ec.ImportECPrivateKey(aes.DecryptCbc(data, iv), out _);
        return ec;
    }
}
