using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Upvest.Signing;

/// <summary>
/// Loads the ECDSA signing key (P-521) from a PEM file. Supports the common PEM shapes an
/// operator may provide: an OpenSSL legacy encrypted SEC1 key (<c>EC PRIVATE KEY</c> with
/// <c>Proc-Type</c>/<c>DEK-Info</c>), an unencrypted SEC1 key, and PKCS#8 (encrypted or not).
/// </summary>
public static class UpvestKeyLoader
{
    public static ECDsa Load(string pem, string? passphrase)
    {
        if (string.IsNullOrWhiteSpace(pem))
        {
            throw new ArgumentException("The signing key PEM is empty.", nameof(pem));
        }

        var ecdsa = ECDsa.Create();

        // OpenSSL legacy encrypted SEC1 key: "BEGIN EC PRIVATE KEY" + "Proc-Type: 4,ENCRYPTED".
        if (pem.Contains("EC PRIVATE KEY") && pem.Contains("ENCRYPTED"))
        {
            var der = DecryptLegacyPem(pem, passphrase ?? string.Empty);
            ecdsa.ImportECPrivateKey(der, out _);
            return ecdsa;
        }

        try
        {
            if (pem.Contains("ENCRYPTED PRIVATE KEY"))
            {
                ecdsa.ImportFromEncryptedPem(pem, passphrase ?? string.Empty);
            }
            else
            {
                // Handles "EC PRIVATE KEY" (SEC1) and "PRIVATE KEY" (PKCS#8), both unencrypted.
                ecdsa.ImportFromPem(pem);
            }
            return ecdsa;
        }
        catch
        {
            ecdsa.Dispose();
            throw;
        }
    }

    private static byte[] DecryptLegacyPem(string pem, string passphrase)
    {
        var lines = pem.Replace("\r\n", "\n").Split('\n');

        string? dekInfo = null;
        var body = new StringBuilder();
        bool inBody = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("-----BEGIN"))
            {
                inBody = true;
                continue;
            }
            if (line.StartsWith("-----END"))
            {
                break;
            }
            if (!inBody)
            {
                continue;
            }
            if (line.StartsWith("DEK-Info:", StringComparison.OrdinalIgnoreCase))
            {
                dekInfo = line.Substring("DEK-Info:".Length).Trim();
                continue;
            }
            if (line.StartsWith("Proc-Type:", StringComparison.OrdinalIgnoreCase) || line.Length == 0)
            {
                continue;
            }
            body.Append(line);
        }

        if (dekInfo is null)
        {
            throw new CryptographicException("Encrypted PEM is missing the DEK-Info header.");
        }

        var parts = dekInfo.Split(',', 2);
        var cipher = parts[0].Trim().ToUpperInvariant();
        var iv = Convert.FromHexString(parts[1].Trim());

        int keyLength = cipher switch
        {
            "AES-256-CBC" => 32,
            "AES-192-CBC" => 24,
            "AES-128-CBC" => 16,
            _ => throw new CryptographicException($"Unsupported PEM encryption cipher '{cipher}'.")
        };

        var salt = iv.Take(8).ToArray();
        var key = DeriveKey(Encoding.UTF8.GetBytes(passphrase), salt, keyLength);
        var cipherText = Convert.FromBase64String(body.ToString());

        using var aes = Aes.Create();
        aes.KeySize = keyLength * 8;
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
    }

    /// <summary>OpenSSL's EVP_BytesToKey with MD5, one iteration (the legacy PEM KDF).</summary>
    private static byte[] DeriveKey(byte[] password, byte[] salt, int keyLength)
    {
        var result = new List<byte>();
        byte[] previous = Array.Empty<byte>();

        using var md5 = MD5.Create();
        while (result.Count < keyLength)
        {
            md5.Initialize();
            var input = new byte[previous.Length + password.Length + salt.Length];
            Buffer.BlockCopy(previous, 0, input, 0, previous.Length);
            Buffer.BlockCopy(password, 0, input, previous.Length, password.Length);
            Buffer.BlockCopy(salt, 0, input, previous.Length + password.Length, salt.Length);
            previous = md5.ComputeHash(input);
            result.AddRange(previous);
        }

        return result.Take(keyLength).ToArray();
    }
}
