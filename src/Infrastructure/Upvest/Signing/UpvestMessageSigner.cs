using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest.Signing;

/// <summary>
/// Signs the signature base string for Upvest's HTTP message signatures (draft v15): ECDSA with
/// the P-521 curve and SHA-512, DER-encoded, standard Base64. Holds the private key for the
/// lifetime of the process. The key passphrase is used only to load the key and is never retained
/// in a field or logged.
/// </summary>
public sealed class UpvestMessageSigner : IDisposable
{
    private readonly ECDsa _key;
    private readonly object _lock = new();

    public UpvestMessageSigner(IOptions<UpvestSettings> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SigningKeyPath))
        {
            throw new InvalidOperationException("Upvest:SigningKeyPath is not configured.");
        }
        if (!File.Exists(settings.SigningKeyPath))
        {
            throw new FileNotFoundException("The Upvest signing key file was not found.", settings.SigningKeyPath);
        }

        var pem = File.ReadAllText(settings.SigningKeyPath);
        _key = UpvestKeyLoader.Load(pem, settings.SigningKeyPassphrase);
        KeyId = settings.SigningKeyId;
    }

    /// <summary>The key id Upvest has on file, sent as the <c>keyid</c> signature parameter.</summary>
    public string KeyId { get; }

    /// <summary>Signs the signature base and returns the standard Base64 of the DER ECDSA signature.</summary>
    public string Sign(string signatureBase)
    {
        var bytes = Encoding.UTF8.GetBytes(signatureBase);
        byte[] signature;
        lock (_lock)
        {
            signature = _key.SignData(bytes, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        }
        return Convert.ToBase64String(signature);
    }

    public void Dispose() => _key.Dispose();
}
