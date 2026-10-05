using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>Produces the Upvest HTTP message signature for a prepared signature base.</summary>
public interface IUpvestRequestSigner
{
    /// <summary>
    /// Sign the signature base with the configured EC signing key (ECDSA P-521 / SHA-512) and return the
    /// ASN.1 DER signature, base64-encoded — the value Upvest expects inside the <c>Signature</c> header.
    /// </summary>
    string SignBase(string signatureBase);

    /// <summary>The <c>alg</c> label Upvest's signature scheme uses for this key.</summary>
    string Algorithm { get; }
}

/// <summary>
/// Loads the encrypted PKCS#8 EC private key once and signs request bases with it. The key material and the
/// passphrase never leave this object and are never logged.
/// </summary>
public sealed class UpvestRequestSigner : IUpvestRequestSigner, IDisposable
{
    private readonly ECDsa _key;

    public UpvestRequestSigner(IOptions<UpvestSettings> options)
    {
        var settings = options.Value;
        if (!File.Exists(settings.SigningKeyPath))
        {
            throw new InvalidOperationException(
                $"Upvest:SigningKeyPath points at a file that does not exist. Configure a readable signing key.");
        }

        var pem = File.ReadAllText(settings.SigningKeyPath);
        _key = ECDsa.Create();
        try
        {
            _key.ImportFromEncryptedPem(pem, settings.SigningKeyPassphrase);
        }
        catch (Exception ex)
        {
            _key.Dispose();
            // Never echo the passphrase or key material.
            throw new InvalidOperationException(
                "Upvest signing key could not be loaded. Check Upvest:SigningKeyPath and Upvest:SigningKeyPassphrase.", ex);
        }
    }

    public string Algorithm => "ecdsa-p521-sha512";

    public string SignBase(string signatureBase)
    {
        var der = _key.SignData(
            Encoding.UTF8.GetBytes(signatureBase),
            HashAlgorithmName.SHA512,
            DSASignatureFormat.Rfc3279DerSequence);
        return Convert.ToBase64String(der);
    }

    public void Dispose() => _key.Dispose();
}
