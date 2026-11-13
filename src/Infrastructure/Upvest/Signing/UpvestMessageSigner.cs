using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest.Signing;

/// <summary>
/// Default <see cref="IUpvestMessageSigner"/>: signs the canonical signature base with an
/// ECDSA P-521 key using SHA-512, producing a DER-encoded signature as required by Upvest.
/// The private key is loaded once (decrypting the legacy OpenSSL PEM if needed) and kept as
/// <see cref="ECParameters"/>; a fresh <see cref="ECDsa"/> is created per signature so the
/// signer is safe to use concurrently.
/// </summary>
public class UpvestMessageSigner : IUpvestMessageSigner
{
    private static readonly string[] ComponentOrder =
    {
        "@method", "@path", "@query", "accept", "authorization",
        "content-length", "content-type", "content-digest", "idempotency-key", "upvest-client-id"
    };

    private readonly string _keyId;
    private readonly ECParameters _keyParameters;

    public UpvestMessageSigner(IOptions<UpvestSettings> settings)
    {
        var s = settings.Value;
        _keyId = s.SigningKeyId;
        _keyParameters = LoadPrivateKey(s.SigningKeyPath, s.SigningKeyPassphrase);
    }

    public SignatureHeaders Sign(
        string method,
        string path,
        string? query,
        string accept,
        string? authorization,
        string? contentType,
        long? contentLength,
        string? contentDigest,
        string? idempotencyKey,
        string upvestClientId)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["@method"] = method.ToUpperInvariant(),
            ["@path"] = path,
            ["accept"] = accept,
            ["upvest-client-id"] = upvestClientId
        };

        if (!string.IsNullOrEmpty(query))
        {
            values["@query"] = query.StartsWith('?') ? query : "?" + query;
        }

        if (!string.IsNullOrEmpty(authorization))
        {
            values["authorization"] = authorization;
        }

        if (contentLength.HasValue && !string.IsNullOrEmpty(contentType))
        {
            values["content-length"] = contentLength.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            values["content-type"] = contentType;
            if (!string.IsNullOrEmpty(contentDigest))
            {
                values["content-digest"] = contentDigest;
            }
        }

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            values["idempotency-key"] = idempotencyKey;
        }

        var covered = new List<string>();
        var baseBuilder = new StringBuilder();
        foreach (var component in ComponentOrder)
        {
            if (!values.TryGetValue(component, out var value))
            {
                continue;
            }

            covered.Add(component);
            baseBuilder.Append('"').Append(component).Append("\": ").Append(value).Append('\n');
        }

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = GenerateNonce();
        var componentList = "(" + string.Join(" ", WrapInQuotes(covered)) + ")";
        var signatureParams =
            $"{componentList};keyid=\"{_keyId}\";created={created};nonce=\"{nonce}\"";

        baseBuilder.Append("\"@signature-params\": ").Append(signatureParams);
        var signatureBase = baseBuilder.ToString();

        var signatureBytes = SignData(Encoding.UTF8.GetBytes(signatureBase));
        var signatureValue = Convert.ToBase64String(signatureBytes);

        return new SignatureHeaders(
            Signature: $"sig1=:{signatureValue}:",
            SignatureInput: $"sig1={signatureParams}");
    }

    private byte[] SignData(byte[] data)
    {
        using var ecdsa = ECDsa.Create(_keyParameters);
        return ecdsa.SignData(data, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
    }

    private static IEnumerable<string> WrapInQuotes(IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            yield return "\"" + item + "\"";
        }
    }

    private static string GenerateNonce()
    {
        var bytes = new byte[12];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', 'A').Replace('/', 'B');
    }

    private static ECParameters LoadPrivateKey(string path, string passphrase)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FileNotFoundException(
                "The Upvest signing key file was not found. Check the Upvest:SigningKeyPath setting.", path);
        }

        var pem = File.ReadAllText(path);
        using var ecdsa = ECDsa.Create();

        if (pem.Contains("BEGIN EC PRIVATE KEY", StringComparison.Ordinal))
        {
            // Legacy OpenSSL SEC1 key (possibly AES-256-CBC encrypted). Decrypt to DER ourselves.
            var der = LegacyEcPemDecryptor.DecryptToDer(pem, passphrase);
            ecdsa.ImportECPrivateKey(der, out _);
        }
        else if (pem.Contains("BEGIN ENCRYPTED PRIVATE KEY", StringComparison.Ordinal))
        {
            ecdsa.ImportFromEncryptedPem(pem, passphrase);
        }
        else
        {
            ecdsa.ImportFromPem(pem);
        }

        return ecdsa.ExportParameters(includePrivateParameters: true);
    }
}
