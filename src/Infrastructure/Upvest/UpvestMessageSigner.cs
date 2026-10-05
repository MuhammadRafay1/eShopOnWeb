using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Signs outgoing requests to Upvest using HTTP Message Signatures (v15): ECDSA P-521 / SHA-512,
/// with a SHA-512 content-digest. Adds the <c>signature-input</c>, <c>signature</c> and
/// <c>content-digest</c> headers. Reads every component's value straight off the request so the
/// signed bytes always match what is sent on the wire.
/// </summary>
public sealed class UpvestMessageSigner : IDisposable
{
    private readonly UpvestOptions _options;
    private readonly ECDsa _signingKey;
    private readonly object _signLock = new();

    public UpvestMessageSigner(IOptions<UpvestOptions> options)
    {
        _options = options.Value;

        var pem = File.ReadAllText(_options.SigningKeyPath);
        _signingKey = ECDsa.Create();
        _signingKey.ImportFromEncryptedPem(pem, _options.SigningKeyPassphrase);
    }

    /// <summary>
    /// Compute and attach the signature headers for <paramref name="request"/>. All other request
    /// headers (accept, authorization, upvest-client-id, idempotency-key, content-type) must already
    /// be set; this method adds content-digest, signature-input and signature.
    /// </summary>
    public async Task SignAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
            throw new InvalidOperationException("Cannot sign a request without a URI.");

        var components = new List<(string Key, string Value)>
        {
            ("@method", request.Method.Method.ToUpperInvariant()),
            ("@path", request.RequestUri.AbsolutePath),
        };

        var query = request.RequestUri.Query; // includes leading '?'
        if (!string.IsNullOrEmpty(query))
            components.Add(("@query", query));

        if (request.Headers.Accept.Count > 0)
            components.Add(("accept", request.Headers.Accept.ToString()));

        if (request.Headers.Authorization is not null)
            components.Add(("authorization", request.Headers.Authorization.ToString()));

        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsByteArrayAsync(cancellationToken);

            components.Add(("content-length", body.Length.ToString()));

            var contentType = request.Content.Headers.ContentType?.ToString();
            if (!string.IsNullOrEmpty(contentType))
                components.Add(("content-type", contentType));

            var digest = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":";
            request.Headers.TryAddWithoutValidation("content-digest", digest);
            components.Add(("content-digest", digest));
        }

        if (request.Headers.TryGetValues("idempotency-key", out var idemValues))
            components.Add(("idempotency-key", string.Join(", ", idemValues)));

        if (request.Headers.TryGetValues("upvest-client-id", out var clientIdValues))
            components.Add(("upvest-client-id", string.Join(", ", clientIdValues)));

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = GenerateNonce();

        var keyList = new StringBuilder("(");
        for (var i = 0; i < components.Count; i++)
        {
            if (i > 0) keyList.Append(' ');
            keyList.Append('"').Append(components[i].Key).Append('"');
        }
        keyList.Append(')');

        var signatureParams =
            $"{keyList};keyid=\"{_options.SigningKeyId}\";created={created};nonce=\"{nonce}\"";

        var baseBuilder = new StringBuilder();
        foreach (var (key, value) in components)
            baseBuilder.Append('"').Append(key).Append("\": ").Append(value).Append('\n');
        baseBuilder.Append("\"@signature-params\": ").Append(signatureParams);

        var signatureBase = baseBuilder.ToString();
        var signature = Sign(signatureBase);

        request.Headers.TryAddWithoutValidation("signature-input", $"sig1={signatureParams}");
        request.Headers.TryAddWithoutValidation("signature", $"sig1=:{signature}:");
    }

    private string Sign(string signatureBase)
    {
        var bytes = Encoding.UTF8.GetBytes(signatureBase);
        byte[] der;
        lock (_signLock)
        {
            der = _signingKey.SignData(bytes, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        }
        return Convert.ToBase64String(der);
    }

    private static string GenerateNonce()
    {
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .Replace("+", "").Replace("/", "").Replace("=", "");
    }

    public void Dispose() => _signingKey.Dispose();
}
