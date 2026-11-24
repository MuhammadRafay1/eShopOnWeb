using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Loads the Upvest request-signing private key (ECDSA P-521) from a PEM file and exposes it for
/// signing. Supports unencrypted PEM, PKCS#8 encrypted PEM, and legacy OpenSSL "traditional"
/// encrypted SEC1 PEM (the <c>Proc-Type: 4,ENCRYPTED</c> / <c>DEK-Info</c> form, which .NET cannot
/// import natively) so the key issued by Upvest works as-is.
/// </summary>
public sealed class UpvestSigningKey : IDisposable
{
    private readonly ECDsa _key;
    private readonly object _sync = new();

    public string KeyId { get; }

    public UpvestSigningKey(string keyId, ECDsa key)
    {
        KeyId = keyId;
        _key = key;
    }

    /// <summary>
    /// Signs the data with ECDSA/SHA-512, DER-encoded (RFC 9421 "v15"). Synchronised because a single
    /// <see cref="ECDsa"/> instance is shared across requests and is not guaranteed thread-safe.
    /// </summary>
    public byte[] SignData(byte[] data)
    {
        lock (_sync)
        {
            return _key.SignData(data, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        }
    }

    public static UpvestSigningKey Load(UpvestSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SigningKeyPath))
        {
            throw new InvalidOperationException("Upvest:SigningKeyPath is not configured.");
        }
        if (!File.Exists(settings.SigningKeyPath))
        {
            throw new FileNotFoundException($"Upvest signing key not found at the configured path.", settings.SigningKeyPath);
        }

        var pem = File.ReadAllText(settings.SigningKeyPath);
        var ecdsa = ECDsa.Create();

        try
        {
            if (IsLegacyEncryptedPem(pem))
            {
                var der = DecryptLegacyPem(pem, settings.SigningKeyPassphrase);
                ecdsa.ImportECPrivateKey(der, out _);
            }
            else if (pem.Contains("ENCRYPTED PRIVATE KEY", StringComparison.Ordinal))
            {
                ecdsa.ImportFromEncryptedPem(pem, settings.SigningKeyPassphrase);
            }
            else
            {
                ecdsa.ImportFromPem(pem);
            }
        }
        catch
        {
            ecdsa.Dispose();
            throw;
        }

        return new UpvestSigningKey(settings.SigningKeyId, ecdsa);
    }

    private static bool IsLegacyEncryptedPem(string pem) =>
        pem.Contains("Proc-Type: 4,ENCRYPTED", StringComparison.Ordinal) &&
        pem.Contains("DEK-Info:", StringComparison.Ordinal);

    /// <summary>
    /// Decrypts a legacy OpenSSL traditional encrypted PEM (AES-CBC) into the raw SEC1 DER bytes.
    /// Mirrors OpenSSL's EVP_BytesToKey (MD5) key derivation.
    /// </summary>
    private static byte[] DecryptLegacyPem(string pem, string passphrase)
    {
        string? dekInfo = null;
        var base64 = new StringBuilder();
        var inBody = false;
        var headersDone = false;

        foreach (var rawLine in pem.Split('\n'))
        {
            var line = rawLine.Trim('\r', ' ', '\t');
            if (line.StartsWith("-----BEGIN", StringComparison.Ordinal))
            {
                inBody = true;
                continue;
            }
            if (line.StartsWith("-----END", StringComparison.Ordinal))
            {
                break;
            }
            if (!inBody)
            {
                continue;
            }

            if (!headersDone)
            {
                if (line.StartsWith("DEK-Info:", StringComparison.Ordinal))
                {
                    dekInfo = line.Substring("DEK-Info:".Length).Trim();
                    continue;
                }
                if (line.StartsWith("Proc-Type:", StringComparison.Ordinal))
                {
                    continue;
                }
                if (line.Length == 0)
                {
                    headersDone = true;
                    continue;
                }
                // A non-empty, non-header line marks the start of the body.
                headersDone = true;
            }

            base64.Append(line);
        }

        if (dekInfo == null)
        {
            throw new CryptographicException("Encrypted PEM is missing its DEK-Info header.");
        }

        // DEK-Info: <cipher>,<hex-iv>
        var parts = dekInfo.Split(',', 2);
        var cipher = parts[0].Trim().ToUpperInvariant();
        var iv = Convert.FromHexString(parts[1].Trim());
        var cipherText = Convert.FromBase64String(base64.ToString());

        var keyLength = cipher switch
        {
            "AES-256-CBC" => 32,
            "AES-192-CBC" => 24,
            "AES-128-CBC" => 16,
            _ => throw new CryptographicException($"Unsupported PEM cipher '{cipher}'.")
        };

        // OpenSSL uses the first 8 bytes of the IV as the salt for EVP_BytesToKey.
        var salt = new byte[8];
        Array.Copy(iv, salt, 8);
        var key = DeriveKeyEvpBytesToKey(Encoding.UTF8.GetBytes(passphrase ?? string.Empty), salt, keyLength);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
    }

    private static byte[] DeriveKeyEvpBytesToKey(byte[] password, byte[] salt, int keyLength)
    {
        var result = new List<byte>(keyLength);
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

        return result.GetRange(0, keyLength).ToArray();
    }

    public void Dispose() => _key.Dispose();
}
