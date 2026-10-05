using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Investing.Http;

/// <summary>
/// Loads the passphrase-protected EC private key once and signs request signature bases with it.
/// Upvest accepts ECDSA on the P-521 curve with SHA-512 hashing; the signature is DER-encoded
/// (as in the Upvest examples) then Base64-encoded.
/// </summary>
public sealed class UpvestMessageSigner : IUpvestMessageSigner, IDisposable
{
    private readonly ECDsa _ecdsa;

    public UpvestMessageSigner(IOptions<UpvestSettings> options)
    {
        var settings = options.Value;
        KeyId = settings.SigningKeyId;

        var pem = File.ReadAllText(settings.SigningKeyPath);
        _ecdsa = ECDsa.Create();
        // The key is a PEM "ENCRYPTED PRIVATE KEY" (PKCS#8); decrypt it with the passphrase.
        _ecdsa.ImportFromEncryptedPem(pem, settings.SigningKeyPassphrase);
    }

    public string KeyId { get; }

    public string SignToBase64(string signatureBase)
    {
        var data = Encoding.UTF8.GetBytes(signatureBase);
        var signature = _ecdsa.SignData(data, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        return Convert.ToBase64String(signature);
    }

    public void Dispose() => _ecdsa.Dispose();
}
