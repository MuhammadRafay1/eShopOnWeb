using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Signs outgoing Upvest requests with HTTP Message Signatures v15
/// (ECDSA P-521 / SHA-512, DER-encoded). The signing key is loaded once.
/// </summary>
public sealed class UpvestRequestSigner : IDisposable
{
    private readonly UpvestOptions _options;
    private readonly ECDsa _key;

    public UpvestRequestSigner(IOptions<UpvestOptions> options)
    {
        _options = options.Value;
        _key = ECDsa.Create();
        _key.ImportFromEncryptedPem(File.ReadAllText(_options.SigningKeyPath), _options.SigningKeyPassphrase);
    }

    /// <summary>
    /// Adds the mandatory Upvest headers (date, client id, api/signature version,
    /// accept, content-digest) and the computed signature / signature-input
    /// headers. Reads the Authorization header already set on the request, if any.
    /// </summary>
    public async Task SignAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        request.Headers.Date = now;
        if (!request.Headers.Contains("upvest-client-id")) request.Headers.TryAddWithoutValidation("upvest-client-id", _options.ClientId);
        if (!request.Headers.Contains("upvest-api-version")) request.Headers.TryAddWithoutValidation("upvest-api-version", "1");
        if (request.Headers.Accept.Count == 0) request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!request.Headers.Contains("upvest-signature-version")) request.Headers.TryAddWithoutValidation("upvest-signature-version", "15");

        byte[] body = request.Content is not null
            ? await request.Content.ReadAsByteArrayAsync(ct)
            : Array.Empty<byte>();

        string? contentType = null, contentDigest = null;
        if (request.Content is not null)
        {
            contentType = request.Content.Headers.ContentType?.ToString();
            request.Content.Headers.ContentLength = body.Length;
            if (body.Length > 0)
            {
                contentDigest = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":";
                request.Content.Headers.TryAddWithoutValidation("content-digest", contentDigest);
            }
        }

        // Canonical component order; include only components that are present.
        var components = new List<(string Key, string Value)>
        {
            ("@method", request.Method.Method.ToUpperInvariant()),
            ("@path", request.RequestUri!.AbsolutePath),
        };
        var query = request.RequestUri!.Query;
        if (!string.IsNullOrEmpty(query)) components.Add(("@query", query));
        components.Add(("accept", "application/json"));
        if (request.Headers.Authorization is not null) components.Add(("authorization", request.Headers.Authorization.ToString()));
        if (body.Length > 0)
        {
            components.Add(("content-length", body.Length.ToString()));
            components.Add(("content-type", contentType!));
            components.Add(("content-digest", contentDigest!));
        }
        if (request.Headers.TryGetValues("idempotency-key", out var idempotencyKey)) components.Add(("idempotency-key", idempotencyKey.First()));
        components.Add(("upvest-client-id", _options.ClientId));

        var created = now.ToUnixTimeSeconds();
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var keyList = "(" + string.Join(" ", components.Select(c => "\"" + c.Key + "\"")) + ")";
        var signatureParams = $"{keyList};keyid=\"{_options.SigningKeyId}\";created={created};nonce=\"{nonce}\"";

        var baseString = new StringBuilder();
        foreach (var c in components) baseString.Append('"').Append(c.Key).Append("\": ").Append(c.Value).Append('\n');
        baseString.Append("\"@signature-params\": ").Append(signatureParams);

        var signature = _key.SignData(
            Encoding.UTF8.GetBytes(baseString.ToString()),
            HashAlgorithmName.SHA512,
            DSASignatureFormat.Rfc3279DerSequence);

        request.Headers.TryAddWithoutValidation("signature-input", "sig1=" + signatureParams);
        request.Headers.TryAddWithoutValidation("signature", "sig1=:" + Convert.ToBase64String(signature) + ":");
    }

    public void Dispose() => _key.Dispose();
}
