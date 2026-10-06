using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Computes the HTTP Message Signature (RFC 9421 style) that Upvest requires on every call: a
/// <c>signature-input</c> metadata header and a <c>signature</c> header. The signing key is an EC private
/// key (P-521 → ECDSA/SHA-512) loaded once from an encrypted PEM. The passphrase and key material never
/// leave this type and are never logged.
///
/// The covered components, algorithm label and signature encoding are read from configuration so the
/// exact scheme the provider verifies can be adjusted without a code change.
/// </summary>
public sealed class UpvestRequestSigner : IDisposable
{
    private readonly ECDsa _key;
    private readonly HashAlgorithmName _hash;
    private readonly string _keyId;
    private readonly string _algLabel;
    private readonly string[] _components;
    private readonly DSASignatureFormat _format;
    private readonly object _gate = new();

    public UpvestRequestSigner(UpvestOptions options)
    {
        _keyId = options.SigningKeyId;

        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromEncryptedPem(File.ReadAllText(options.SigningKeyPath), options.SigningKeyPassphrase);
        _key = ecdsa;

        var (hash, defaultAlg) = _key.KeySize switch
        {
            <= 256 => (HashAlgorithmName.SHA256, "ecdsa-p256-sha256"),
            <= 384 => (HashAlgorithmName.SHA384, "ecdsa-p384-sha384"),
            _ => (HashAlgorithmName.SHA512, "ecdsa-p521-sha512"),
        };
        _hash = hash;
        _algLabel = string.IsNullOrWhiteSpace(options.SignatureAlg) ? defaultAlg : options.SignatureAlg;

        _components = (options.SignatureComponents ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (_components.Length == 0)
            _components = new[] { "@method", "@path", "@authority" };

        _format = string.Equals(options.SignatureFormat, "der", StringComparison.OrdinalIgnoreCase)
            ? DSASignatureFormat.Rfc3279DerSequence
            : DSASignatureFormat.IeeeP1363FixedFieldConcatenation;
    }

    public readonly record struct SignatureHeaders(string SignatureInput, string Signature);

    /// <summary>Builds the <c>signature-input</c> and <c>signature</c> header values for a request.</summary>
    public SignatureHeaders Sign(string httpMethod, Uri requestUri, DateTimeOffset createdAt)
    {
        var created = createdAt.ToUnixTimeSeconds();

        string ComponentValue(string component) => component switch
        {
            "@method" => httpMethod.ToUpperInvariant(),
            "@authority" => requestUri.Authority.ToLowerInvariant(),
            "@path" => requestUri.AbsolutePath,
            "@query" => string.IsNullOrEmpty(requestUri.Query) ? "?" : requestUri.Query,
            "@request-target" => requestUri.PathAndQuery,
            "@target-uri" => requestUri.AbsoluteUri,
            _ => string.Empty,
        };

        var coveredList = string.Join(" ", _components.Select(c => $"\"{c}\""));
        var sigParams = $"({coveredList});created={created};keyid=\"{_keyId}\";alg=\"{_algLabel}\"";

        var baseBuilder = new StringBuilder();
        foreach (var component in _components)
            baseBuilder.Append($"\"{component}\": {ComponentValue(component)}\n");
        baseBuilder.Append($"\"@signature-params\": {sigParams}");

        byte[] signature;
        lock (_gate)
        {
            signature = _key.SignData(Encoding.UTF8.GetBytes(baseBuilder.ToString()), _hash, _format);
        }

        return new SignatureHeaders(
            SignatureInput: $"sig1={sigParams}",
            Signature: $"sig1=:{Convert.ToBase64String(signature)}:");
    }

    public void Dispose() => _key.Dispose();
}
