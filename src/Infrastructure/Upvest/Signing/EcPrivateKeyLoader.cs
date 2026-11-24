using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Upvest.Signing;

/// <summary>
/// Loads a PEM-encoded EC private key, supporting three encodings:
/// <list type="bullet">
/// <item>PKCS#8 encrypted ("ENCRYPTED PRIVATE KEY") via the framework loader;</item>
/// <item>legacy OpenSSL SEC1 encrypted ("EC PRIVATE KEY" with a <c>Proc-Type: 4,ENCRYPTED</c>
/// / <c>DEK-Info</c> header), decrypted here because the framework cannot read that format;</item>
/// <item>unencrypted PKCS#8 / SEC1.</item>
/// </list>
/// The decrypted key material never leaves memory.
/// </summary>
public static class EcPrivateKeyLoader
{
    public static ECDsa Load(string pem, string? passphrase)
    {
        if (string.IsNullOrWhiteSpace(pem))
            throw new ArgumentException("PEM content is empty.", nameof(pem));

        var ecdsa = ECDsa.Create();

        if (pem.Contains("Proc-Type: 4,ENCRYPTED", StringComparison.Ordinal))
        {
            var der = DecryptLegacyPem(pem, passphrase ?? string.Empty);
            ecdsa.ImportECPrivateKey(der, out _);
            return ecdsa;
        }

        if (pem.Contains("ENCRYPTED PRIVATE KEY", StringComparison.Ordinal))
        {
            ecdsa.ImportFromEncryptedPem(pem, passphrase ?? string.Empty);
            return ecdsa;
        }

        ecdsa.ImportFromPem(pem);
        return ecdsa;
    }

    /// <summary>
    /// Decrypts a legacy OpenSSL-encrypted "EC PRIVATE KEY" PEM block (PEM-level encryption
    /// with a <c>DEK-Info</c> header) and returns the SEC1 DER key bytes.
    /// </summary>
    private static byte[] DecryptLegacyPem(string pem, string passphrase)
    {
        string? dekInfo = null;
        var base64 = new StringBuilder();
        var inBody = false;

        foreach (var rawLine in pem.Split('\n'))
        {
            var line = rawLine.Trim('\r', ' ', '\t');
            if (line.Length == 0)
            {
                // The blank line separates the PEM headers from the base64 body.
                if (dekInfo != null) inBody = true;
                continue;
            }
            if (line.StartsWith("-----BEGIN", StringComparison.Ordinal)) continue;
            if (line.StartsWith("-----END", StringComparison.Ordinal)) break;
            if (line.StartsWith("Proc-Type:", StringComparison.Ordinal)) continue;
            if (line.StartsWith("DEK-Info:", StringComparison.Ordinal))
            {
                dekInfo = line.Substring("DEK-Info:".Length).Trim();
                continue;
            }
            if (dekInfo != null) inBody = true;
            if (inBody) base64.Append(line);
        }

        if (dekInfo == null)
            throw new CryptographicException("Encrypted PEM is missing its DEK-Info header.");

        // DEK-Info: <cipher>,<iv-hex>
        var parts = dekInfo.Split(',', 2);
        if (parts.Length != 2)
            throw new CryptographicException($"Malformed DEK-Info header: {dekInfo}");

        var cipherName = parts[0].Trim();
        var iv = HexToBytes(parts[1].Trim());
        var cipherText = Convert.FromBase64String(base64.ToString());

        if (!cipherName.Equals("AES-256-CBC", StringComparison.OrdinalIgnoreCase))
            throw new CryptographicException($"Unsupported legacy PEM cipher '{cipherName}'. Expected AES-256-CBC.");

        // OpenSSL legacy key derivation: EVP_BytesToKey with MD5, salt = first 8 bytes of IV,
        // 1 iteration, producing a 32-byte AES-256 key.
        var key = DeriveKeyEvpBytesToKey(Encoding.UTF8.GetBytes(passphrase), iv.AsSpan(0, 8).ToArray(), 32);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
    }

    /// <summary>OpenSSL EVP_BytesToKey using MD5 and a single iteration.</summary>
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

    private static byte[] HexToBytes(string hex)
    {
        if (hex.Length % 2 != 0)
            throw new CryptographicException("IV hex length must be even.");
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return bytes;
    }
}
