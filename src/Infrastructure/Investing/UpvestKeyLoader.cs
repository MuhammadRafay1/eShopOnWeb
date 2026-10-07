using System;
using System.Security.Cryptography;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Loads the EC private key Upvest uses for HTTP message signatures. Upvest's
/// key setup can produce three on-disk shapes; this reader handles all of them:
/// a plain SEC1/PKCS#8 key, a PKCS#8 encrypted key (<c>BEGIN ENCRYPTED PRIVATE
/// KEY</c>), and the legacy OpenSSL SEC1 encrypted form that
/// <see cref="ECDsa.ImportFromEncryptedPem(ReadOnlySpan{char}, ReadOnlySpan{char})"/>
/// rejects.
/// </summary>
internal static class UpvestKeyLoader
{
    public static ECDsa LoadEcPrivateKey(string pem, string? passphrase)
    {
        var ec = ECDsa.Create();
        if (!pem.Contains("Proc-Type: 4,ENCRYPTED", StringComparison.Ordinal))
        {
            if (pem.Contains("BEGIN ENCRYPTED PRIVATE KEY"))
            {
                ec.ImportFromEncryptedPem(pem, passphrase);      // PKCS#8, encrypted
            }
            else
            {
                ec.ImportFromPem(pem);                           // SEC1 or PKCS#8, plain
            }

            return ec;
        }

        // Legacy OpenSSL SEC1 encrypted (`openssl ec -aes256`), which ImportFromEncryptedPem rejects.
        var lines = pem.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].Trim();
        }

        var dekLine = Array.Find(lines, l => l.StartsWith("DEK-Info:", StringComparison.Ordinal))
            ?? throw new NotSupportedException("Encrypted PEM is missing a DEK-Info header.");
        var dek = dekLine.Substring("DEK-Info:".Length).Trim().Split(',');
        if (dek[0] != "AES-256-CBC")
        {
            throw new NotSupportedException("Unsupported DEK-Info cipher: " + dek[0]);
        }

        var iv = Convert.FromHexString(dek[1]);
        var base64 = string.Concat(Array.FindAll(lines,
            l => l.Length > 0 && !l.StartsWith("-----", StringComparison.Ordinal) && !l.Contains(':')));
        var data = Convert.FromBase64String(base64);

        var pw = System.Text.Encoding.UTF8.GetBytes(passphrase ?? string.Empty);
        var salt = iv[..8];
        var d1 = MD5.HashData([.. pw, .. salt]);                 // OpenSSL EVP_BytesToKey, MD5
        var d2 = MD5.HashData([.. d1, .. pw, .. salt]);

        using var aes = Aes.Create();
        aes.Key = [.. d1, .. d2];
        ec.ImportECPrivateKey(aes.DecryptCbc(data, iv), out _);
        return ec;
    }
}
