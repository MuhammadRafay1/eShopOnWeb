using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Signs outgoing Upvest requests with HTTP Message Signatures (Upvest v15): ECDSA P-521 / SHA-512, the
/// signature ASN.1-DER encoded, over a signature base built from the covered components Upvest requires.
/// The EC private key is loaded once (it is a secret and is never logged). Thread-safe.
/// </summary>
public sealed class UpvestRequestSigner : IDisposable
{
    private const string Alg = "ecdsa-p521-sha512";

    private readonly ECDsa _key;
    private readonly string _keyId;
    private readonly object _signLock = new();

    public UpvestRequestSigner(UpvestOptions options)
    {
        _keyId = options.SigningKeyId;
        var pem = File.ReadAllText(options.SigningKeyPath);
        _key = ECDsa.Create();
        _key.ImportFromEncryptedPem(pem.AsSpan(), options.SigningKeyPassphrase.AsSpan());
    }

    /// <summary>
    /// Adds the <c>content-digest</c>, <c>signature-input</c> and <c>signature</c> headers (and
    /// <c>upvest-signature-version</c>) to the request. Authentication headers the SDK may have placed
    /// (signature/signature-input) are replaced here. The request's <c>authorization</c>, <c>upvest-client-id</c>
    /// and <c>idempotency-key</c> headers are expected to be already set (by the handler / SDK).
    /// </summary>
    public async Task SignAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var method = request.Method.Method.ToUpperInvariant();
        var uri = request.RequestUri ?? throw new InvalidOperationException("Request has no URI to sign.");
        var path = uri.AbsolutePath;
        var query = uri.Query; // includes leading '?' when present, else empty
        var isTokenEndpoint = path.Equals("/auth/token", StringComparison.OrdinalIgnoreCase);

        // Buffer the body so we can digest exactly the bytes that will be sent, and so the SDK can re-read it.
        byte[]? body = null;
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync();
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            request.Content.Headers.ContentLength = body.Length;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["@method"] = method,
            ["@path"] = path,
            ["@query"] = query, // Upvest expects "?" + rawquery; HttpClient's Query already carries the leading '?'
            ["upvest-client-id"] = GetHeader(request, "upvest-client-id"),
        };

        var comps = new List<string> { "@method", "@path" };
        if (!string.IsNullOrEmpty(query))
        {
            comps.Add("@query");
        }
        if (!isTokenEndpoint)
        {
            values["authorization"] = GetHeader(request, "authorization");
            comps.Add("authorization");
        }
        comps.Add("upvest-client-id");

        if (body is not null)
        {
            var digest = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":";
            SetHeader(request, "content-digest", digest);
            values["content-digest"] = digest;
            values["content-length"] = body.Length.ToString();
            values["content-type"] = request.Content!.Headers.ContentType?.ToString() ?? "application/octet-stream";
            comps.Add("content-length");
            comps.Add("content-type");
            comps.Add("content-digest");
        }

        var idempotencyKey = GetHeader(request, "idempotency-key");
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            values["idempotency-key"] = idempotencyKey;
            comps.Add("idempotency-key");
        }

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var componentList = "(" + string.Join(" ", comps.Select(c => $"\"{c}\"")) + ")";
        var signatureParams = $"{componentList};created={created};keyid=\"{_keyId}\";alg=\"{Alg}\"";

        var baseBuilder = new StringBuilder();
        foreach (var c in comps)
        {
            baseBuilder.Append('"').Append(c).Append("\": ").Append(values[c]).Append('\n');
        }
        baseBuilder.Append("\"@signature-params\": ").Append(signatureParams);

        var signature = Sign(Encoding.UTF8.GetBytes(baseBuilder.ToString()));

        SetHeader(request, "signature-input", "sig1=" + signatureParams);
        SetHeader(request, "signature", "sig1=:" + signature + ":");
        SetHeader(request, "upvest-signature-version", "15");
    }

    private string Sign(byte[] data)
    {
        lock (_signLock)
        {
            var der = _key.SignData(data, HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
            return Convert.ToBase64String(der);
        }
    }

    private static string GetHeader(HttpRequestMessage request, string name)
    {
        if (request.Headers.TryGetValues(name, out var values))
        {
            return string.Join(", ", values);
        }
        return string.Empty;
    }

    private static void SetHeader(HttpRequestMessage request, string name, string value)
    {
        request.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, value);
    }

    public void Dispose() => _key.Dispose();
}
