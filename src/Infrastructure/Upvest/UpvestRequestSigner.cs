using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>Computes the Upvest HTTP message signature for an outgoing request.</summary>
public interface IUpvestRequestSigner
{
    /// <summary>The <c>upvest-client-id</c> header value every call must carry.</summary>
    string ClientId { get; }

    /// <summary>
    /// Builds the <c>signature-input</c> and <c>signature</c> header values (and the body digest, when a
    /// body is present) over the covered components Upvest requires.
    /// </summary>
    UpvestSignature Sign(
        string method, string path, string? query, byte[] body, string? contentType,
        string? authorization, string? idempotencyKey);
}

/// <summary>The signature headers to attach to a request.</summary>
public readonly record struct UpvestSignature(string SignatureInput, string Signature, string? Digest);

/// <summary>
/// RFC-9421-style HTTP message signatures using the Upvest-registered EC P-521 key (ECDSA / SHA-512, ASN.1
/// DER, base64). The signature base is built from the exact covered components Upvest verifies: method,
/// path, <c>upvest-client-id</c>, the bearer <c>authorization</c> (except on the token endpoint), the query
/// when present, and the content-length/content-type/digest trio when there is a body — plus
/// <c>idempotency-key</c> when the request carries one. The private key is loaded and decrypted once at
/// construction (fail-fast on a bad passphrase).
/// </summary>
public sealed class UpvestRequestSigner : IUpvestRequestSigner, IDisposable
{
    private readonly ECDsa _ecdsa;
    private readonly string _keyId;

    public UpvestRequestSigner(IOptions<UpvestOptions> options)
    {
        var o = options.Value;
        ClientId = o.ClientId;
        _keyId = o.SigningKeyId;

        var pem = File.ReadAllText(o.SigningKeyPath);
        _ecdsa = ECDsa.Create();
        _ecdsa.ImportFromEncryptedPem(pem, o.SigningKeyPassphrase);
    }

    public string ClientId { get; }

    public UpvestSignature Sign(
        string method, string path, string? query, byte[] body, string? contentType,
        string? authorization, string? idempotencyKey)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expires = created + 60;
        var nonce = Guid.NewGuid().ToString("N");

        var components = new List<(string Name, string Value)>
        {
            ("@method", method),
            ("@path", path),
            ("upvest-client-id", ClientId),
        };

        var isTokenEndpoint = path.EndsWith("/auth/token", StringComparison.Ordinal);
        if (!isTokenEndpoint && authorization is not null)
            components.Add(("authorization", authorization));

        if (!string.IsNullOrEmpty(query))
            components.Add(("@query", "?" + query));

        string? digest = null;
        if (body.Length > 0)
        {
            digest = "SHA-256=" + Convert.ToBase64String(SHA256.HashData(body));
            components.Add(("content-length", body.Length.ToString()));
            components.Add(("content-type", contentType ?? ""));
            components.Add(("digest", digest));
        }

        if (idempotencyKey is not null)
            components.Add(("idempotency-key", idempotencyKey));

        var paramList = "(" + string.Join(" ", components.Select(c => $"\"{c.Name}\"")) + ")"
            + $";keyid=\"{_keyId}\";created={created};nonce=\"{nonce}\";expires={expires}";

        var signatureBase = string.Join("\n", components.Select(c => $"\"{c.Name}\": {c.Value}"))
            + "\n\"@signature-params\": " + paramList;

        var der = _ecdsa.SignData(
            Encoding.UTF8.GetBytes(signatureBase), HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);

        return new UpvestSignature(
            SignatureInput: "sig1=" + paramList,
            Signature: "sig1=:" + Convert.ToBase64String(der) + ":",
            Digest: digest);
    }

    public void Dispose() => _ecdsa.Dispose();
}
