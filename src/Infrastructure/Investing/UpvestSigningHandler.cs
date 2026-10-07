using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// The one reusable handler through which every call this application makes to
/// Upvest passes. It authenticates each request by attaching an Upvest HTTP
/// message signature (Upvest refuses any unsigned call, the OAuth token request
/// included) and rewrites the request onto the configured base URL. No call
/// site attaches credentials itself.
///
/// The signing scheme (ECDSA over SHA-512, DER-encoded; the covered-component
/// and parameter rules) follows Upvest's own signer.
/// </summary>
public sealed class UpvestSigningHandler : DelegatingHandler
{
    // Header name prefixes Upvest excludes from the signature.
    private static readonly string[] IgnoredPrefixes =
    {
        "cf-", "cdn-", "cookie", "x-", "priority", "upvest-signature", "sec-",
        "user-agent", "accept-encoding", "connection", "host", "expect", "te", "transfer-encoding"
    };

    private readonly ECDsa _key;
    private readonly string _keyId;
    private readonly string _clientId;
    private readonly Uri _baseUrl;

    public UpvestSigningHandler(ECDsa key, string keyId, string clientId, Uri baseUrl, HttpMessageHandler inner)
        : base(inner)
    {
        _key = key;
        _keyId = keyId;
        _clientId = clientId;
        _baseUrl = baseUrl;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        // Rewrite onto the configured base URL first — the signature covers the path actually sent.
        req.RequestUri = new Uri(_baseUrl, req.RequestUri!.PathAndQuery);

        if (req.Content?.Headers.ContentType is { MediaType: "application/json" } ctype)
        {
            ctype.CharSet = null;
        }

        byte[]? body = req.Content is null ? null : await req.Content.ReadAsByteArrayAsync(ct);

        var accept = req.Headers.Accept.ToString();
        if (accept != "application/json" && accept != "application/pdf")
        {
            req.Headers.Accept.Clear();
            req.Headers.Accept.ParseAdd("application/json");
        }

        if (!req.Headers.Contains("upvest-api-version"))
        {
            req.Headers.TryAddWithoutValidation("upvest-api-version", "1");
        }

        // This application's client id authenticates the call; set it ourselves so no
        // call site has to, overriding whatever the SDK placed there.
        req.Headers.Remove("upvest-client-id");
        req.Headers.TryAddWithoutValidation("upvest-client-id", _clientId);

        // Upvest requires an idempotency key on every mutating call. The SDK supplies one
        // for most operations but omits it on a few (e.g. creating a user check); fill it in
        // here so no call site has to care, and so it is covered by the signature.
        if (IsMutating(req.Method) && !req.Headers.Contains("idempotency-key"))
        {
            req.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());
        }

        var now = DateTimeOffset.UtcNow;
        req.Headers.Date = now;

        if (body is { Length: > 0 })
        {
            req.Content!.Headers.Remove("content-digest");
            req.Content.Headers.TryAddWithoutValidation(
                "content-digest", "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":");
            req.Content.Headers.ContentLength = body.Length;
        }

        var components = new List<(string Name, string Value)>
        {
            ("@method", req.Method.Method.ToUpperInvariant()),
            ("@path", string.IsNullOrEmpty(req.RequestUri.AbsolutePath) ? "/" : req.RequestUri.AbsolutePath)
        };
        if (req.RequestUri.Query.Length > 1)
        {
            components.Add(("@query", req.RequestUri.Query));
        }

        IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers = req.Headers;
        if (req.Content is not null)
        {
            headers = headers.Concat(req.Content.Headers);
        }

        foreach (var h in headers)
        {
            var name = h.Key.ToLowerInvariant();
            if (!IgnoredPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            {
                components.Add((name, string.Join(", ", h.Value)));
            }
        }

        long created = now.ToUnixTimeSeconds();
        var sigParams = "(" + string.Join(" ", components.Select(c => $"\"{c.Name}\"")) + ")"
            + $";keyid=\"{_keyId}\";nonce=\"{Guid.NewGuid()}\";created={created};expires={created + 10}";
        var sigBase = string.Join("\n", components.Select(c => $"\"{c.Name}\": {c.Value}"))
            + "\n\"@signature-params\": " + sigParams;

        var signature = _key.SignData(
            Encoding.UTF8.GetBytes(sigBase), HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);

        req.Headers.TryAddWithoutValidation("signature-input", "sig1=" + sigParams);
        req.Headers.TryAddWithoutValidation("signature", "sig1=:" + Convert.ToBase64String(signature) + ":");
        req.Headers.TryAddWithoutValidation("upvest-signature-version", "15");

        return await base.SendAsync(req, ct);
    }

    private static bool IsMutating(HttpMethod method) =>
        method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _key.Dispose();
        }

        base.Dispose(disposing);
    }
}
