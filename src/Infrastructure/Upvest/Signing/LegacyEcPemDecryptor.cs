using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Upvest.Signing;

/// <summary>
/// Decrypts a legacy OpenSSL-encrypted SEC1 EC private key
/// (<c>-----BEGIN EC PRIVATE KEY-----</c> with <c>Proc-Type: 4,ENCRYPTED</c> and a
/// <c>DEK-Info: AES-256-CBC,&lt;iv&gt;</c> header) into the raw DER bytes of the SEC1
/// <c>ECPrivateKey</c> structure, which .NET can then import with
/// <see cref="System.Security.Cryptography.ECDsa.ImportECPrivateKey(ReadOnlySpan{byte}, out int)"/>.
/// <para>
/// .NET cannot natively import this legacy format (it only understands PKCS#8 encrypted PEM),
/// so the OpenSSL key-derivation (EVP_BytesToKey with MD5) and AES-256-CBC decryption are done here.
/// </para>
/// </summary>
public static class LegacyEcPemDecryptor
{
    private const string EcHeader = "-----BEGIN EC PRIVATE KEY-----";
    private const string EcFooter = "-----END EC PRIVATE KEY-----";

    /// <summary>
    /// Returns the DER bytes of the decrypted SEC1 EC private key. If the PEM is not
    /// encrypted, returns its DER body unchanged. Only the legacy EC PRIVATE KEY PEM is handled;
    /// PKCS#8 (encrypted or not) should be imported directly by the caller.
    /// </summary>
    public static byte[] DecryptToDer(string pem, string passphrase)
    {
        if (string.IsNullOrWhiteSpace(pem))
        {
            throw new ArgumentException("PEM content is empty.", nameof(pem));
        }

        var lines = pem.Replace("\r\n", "\n").Split('\n');
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var body = new StringBuilder();
        var inBlock = false;
        var headerSectionDone = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                if (inBlock)
                {
                    headerSectionDone = true;
                }
                continue;
            }

            if (line.StartsWith(EcHeader, StringComparison.Ordinal))
            {
                inBlock = true;
                continue;
            }

            if (line.StartsWith(EcFooter, StringComparison.Ordinal))
            {
                break;
            }

            if (!inBlock)
            {
                continue;
            }

            if (!headerSectionDone && line.Contains(':'))
            {
                var idx = line.IndexOf(':');
                headers[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
                continue;
            }

            headerSectionDone = true;
            body.Append(line);
        }

        var der = Convert.FromBase64String(body.ToString());

        var isEncrypted = headers.TryGetValue("Proc-Type", out var procType)
            && procType.Replace(" ", string.Empty).Contains("ENCRYPTED", StringComparison.OrdinalIgnoreCase);

        if (!isEncrypted)
        {
            return der;
        }

        if (!headers.TryGetValue("DEK-Info", out var dekInfo))
        {
            throw new InvalidDataException("Encrypted PEM is missing its DEK-Info header.");
        }

        var dekParts = dekInfo.Split(',', 2);
        if (dekParts.Length != 2)
        {
            throw new InvalidDataException($"Unsupported DEK-Info header: '{dekInfo}'.");
        }

        var cipher = dekParts[0].Trim().ToUpperInvariant();
        if (cipher != "AES-256-CBC")
        {
            throw new NotSupportedException(
                $"Only AES-256-CBC encrypted EC keys are supported, got '{cipher}'.");
        }

        var iv = HexToBytes(dekParts[1].Trim());
        if (iv.Length != 16)
        {
            throw new InvalidDataException("AES-256-CBC IV must be 16 bytes.");
        }

        // OpenSSL legacy key derivation: salt = first 8 bytes of the IV.
        var salt = iv.Take(8).ToArray();
        var key = DeriveKeyEvpBytesToKey(Encoding.UTF8.GetBytes(passphrase ?? string.Empty), salt, 32);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(der, 0, der.Length);
    }

    /// <summary>
    /// OpenSSL's EVP_BytesToKey with MD5 and a single iteration (as used for legacy PEM encryption).
    /// </summary>
    private static byte[] DeriveKeyEvpBytesToKey(byte[] passphrase, byte[] salt, int keyLength)
    {
        using var md5 = MD5.Create();
        var result = new List<byte>(keyLength);
        var previous = Array.Empty<byte>();

        while (result.Count < keyLength)
        {
            md5.Initialize();
            var input = new byte[previous.Length + passphrase.Length + salt.Length];
            Buffer.BlockCopy(previous, 0, input, 0, previous.Length);
            Buffer.BlockCopy(passphrase, 0, input, previous.Length, passphrase.Length);
            Buffer.BlockCopy(salt, 0, input, previous.Length + passphrase.Length, salt.Length);
            previous = md5.ComputeHash(input);
            result.AddRange(previous);
        }

        return result.Take(keyLength).ToArray();
    }

    private static byte[] HexToBytes(string hex)
    {
        hex = hex.Trim();
        if (hex.Length % 2 != 0)
        {
            throw new InvalidDataException("Hex string must have an even number of characters.");
        }

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return bytes;
    }
}
