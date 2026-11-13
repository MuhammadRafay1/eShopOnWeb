using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Loads the EC private signing key once and exposes it for signing. Supports both PKCS#8 keys and
/// the legacy OpenSSL-encrypted SEC1 PEM format (<c>Proc-Type: 4,ENCRYPTED</c> / <c>DEK-Info</c>),
/// which the built-in <see cref="ECDsa.ImportFromEncryptedPem(System.ReadOnlySpan{char}, System.ReadOnlySpan{char})"/>
/// does not handle. The key material and passphrase never leave this type.
/// </summary>
public sealed class UpvestSigningKeyProvider : IDisposable
{
    private readonly ECDsa _key;

    public UpvestSigningKeyProvider(IOptions<UpvestSettings> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SigningKeyPath))
        {
            throw new UpvestException("Upvest signing key path is not configured (Upvest:SigningKeyPath).");
        }
        if (!File.Exists(settings.SigningKeyPath))
        {
            throw new UpvestException("Upvest signing key file was not found at the configured path.");
        }

        try
        {
            _key = LoadKey(File.ReadAllText(settings.SigningKeyPath), settings.SigningKeyPassphrase);
        }
        catch (UpvestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Deliberately do not include the exception detail/message chain that could echo key bytes.
            throw new UpvestException("Failed to load the Upvest signing key. Check the key format and passphrase.", ex);
        }
    }

    /// <summary>Signs the ASCII signature base string and returns the base64 of the DER-encoded ECDSA signature.</summary>
    public string SignBase64(string signatureBase)
    {
        var data = Encoding.UTF8.GetBytes(signatureBase);
        var sig = _key.SignData(data, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        return Convert.ToBase64String(sig);
    }

    private static ECDsa LoadKey(string pem, string passphrase)
    {
        var header = DetectHeader(pem);

        // Encrypted PKCS#8 ("BEGIN ENCRYPTED PRIVATE KEY") — handled natively.
        if (header == "ENCRYPTED PRIVATE KEY")
        {
            var ec = ECDsa.Create();
            ec.ImportFromEncryptedPem(pem, passphrase);
            return ec;
        }

        // Plain PKCS#8 / SEC1 (unencrypted) — handled natively.
        if (header is "PRIVATE KEY" or "EC PRIVATE KEY" && !IsLegacyEncrypted(pem))
        {
            var ec = ECDsa.Create();
            ec.ImportFromPem(pem);
            return ec;
        }

        // Legacy OpenSSL-encrypted SEC1 PEM (Proc-Type / DEK-Info).
        return LoadLegacyEncryptedSec1(pem, passphrase);
    }

    private static string DetectHeader(string pem)
    {
        foreach (var raw in pem.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("-----BEGIN ") && line.EndsWith("-----"))
            {
                return line.Substring("-----BEGIN ".Length, line.Length - "-----BEGIN ".Length - "-----".Length).Trim();
            }
        }
        throw new UpvestException("The Upvest signing key is not a recognised PEM file.");
    }

    private static bool IsLegacyEncrypted(string pem) =>
        pem.Contains("Proc-Type:", StringComparison.OrdinalIgnoreCase) &&
        pem.Contains("DEK-Info:", StringComparison.OrdinalIgnoreCase);

    private static ECDsa LoadLegacyEncryptedSec1(string pem, string passphrase)
    {
        string? dekInfo = null;
        var b64 = new StringBuilder();
        var inBody = false;
        var inHeaders = false;

        foreach (var raw in pem.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("-----BEGIN")) { inBody = true; inHeaders = true; continue; }
            if (line.StartsWith("-----END")) break;
            if (!inBody) continue;
            if (inHeaders)
            {
                if (line.StartsWith("Proc-Type:", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("DEK-Info:", StringComparison.OrdinalIgnoreCase))
                {
                    dekInfo = line.Substring("DEK-Info:".Length).Trim();
                    continue;
                }
                if (line.Length == 0) { inHeaders = false; continue; }
                inHeaders = false; // first body line without a blank separator
            }
            b64.Append(line);
        }

        if (dekInfo is null)
        {
            throw new UpvestException("The Upvest signing key looked encrypted but no DEK-Info header was present.");
        }

        var ciphertext = Convert.FromBase64String(b64.ToString());

        // DEK-Info: "<cipher>,<hex-iv>" — only AES-CBC variants are produced by `openssl ec -aes256`.
        var parts = dekInfo.Split(',', 2);
        var cipherName = parts[0].Trim().ToUpperInvariant();
        var iv = Convert.FromHexString(parts[1].Trim());

        var keyLength = cipherName switch
        {
            "AES-256-CBC" => 32,
            "AES-192-CBC" => 24,
            "AES-128-CBC" => 16,
            _ => throw new UpvestException($"Unsupported legacy key cipher '{cipherName}'.")
        };

        // OpenSSL legacy KDF: EVP_BytesToKey(MD5, password, salt = first 8 bytes of IV, 1 iteration).
        var derived = EvpBytesToKeyMd5(Encoding.UTF8.GetBytes(passphrase), iv.Take(8).ToArray(), keyLength);

        byte[] der;
        using (var aes = Aes.Create())
        {
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = derived;
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            der = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        }

        var ecdsa = ECDsa.Create();
        ecdsa.ImportECPrivateKey(der, out _);
        Array.Clear(der);
        Array.Clear(derived);
        return ecdsa;
    }

    private static byte[] EvpBytesToKeyMd5(byte[] password, byte[] salt, int keyLength)
    {
        var result = new List<byte>(keyLength + 16);
        var previous = Array.Empty<byte>();
        using var md5 = MD5.Create();
        while (result.Count < keyLength)
        {
            var buffer = new byte[previous.Length + password.Length + salt.Length];
            Buffer.BlockCopy(previous, 0, buffer, 0, previous.Length);
            Buffer.BlockCopy(password, 0, buffer, previous.Length, password.Length);
            Buffer.BlockCopy(salt, 0, buffer, previous.Length + password.Length, salt.Length);
            previous = md5.ComputeHash(buffer);
            result.AddRange(previous);
        }
        return result.Take(keyLength).ToArray();
    }

    public void Dispose() => _key.Dispose();
}
