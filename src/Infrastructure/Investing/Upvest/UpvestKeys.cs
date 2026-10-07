using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Investing.Upvest;

/// <summary>
/// Loads the EC private key Upvest uses for HTTP message signatures. Handles the three PEM
/// shapes Upvest's key tooling can produce: plain SEC1/PKCS#8, PKCS#8 encrypted
/// (<c>BEGIN ENCRYPTED PRIVATE KEY</c>), and the legacy OpenSSL SEC1-encrypted block
/// (<c>Proc-Type: 4,ENCRYPTED</c>) that <see cref="ECDsa.ImportFromEncryptedPem(string, string)"/>
/// rejects. The passphrase is a secret: it is never logged.
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

        // SEC1 encrypted the legacy OpenSSL way (`openssl ec -aes256`), which
        // ImportFromEncryptedPem rejects because it reads only PKCS#8.
        var lines = pem.Split('\n').Select(l => l.Trim()).ToList();
        var dek = lines.First(l => l.StartsWith("DEK-Info:", StringComparison.Ordinal))
            ["DEK-Info:".Length..].Trim().Split(',');
        if (dek[0] != "AES-256-CBC")
        {
            throw new NotSupportedException("Unsupported DEK-Info cipher: " + dek[0]);
        }

        byte[] iv = Convert.FromHexString(dek[1]);
        byte[] data = Convert.FromBase64String(string.Concat(
            lines.Where(l => l.Length > 0 && !l.StartsWith("-----", StringComparison.Ordinal) && !l.Contains(':'))));
        byte[] pw = Encoding.UTF8.GetBytes(passphrase ?? string.Empty);
        byte[] salt = iv[..8];
        byte[] d1 = MD5.HashData([.. pw, .. salt]);                 // OpenSSL EVP_BytesToKey, MD5
        byte[] d2 = MD5.HashData([.. d1, .. pw, .. salt]);
        using var aes = Aes.Create();
        aes.Key = [.. d1, .. d2];
        ec.ImportECPrivateKey(aes.DecryptCbc(data, iv), out _);
        return ec;
    }
}
