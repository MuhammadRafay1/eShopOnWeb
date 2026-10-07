using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The one reusable handler through which every Upvest request passes. It:
///   1. rewrites the request to the configured <c>Upvest:BaseUrl</c>;
///   2. adds the headers Upvest requires (incl. a fresh idempotency-key on writes);
///   3. signs the request with an HTTP message signature (Upvest v15: ECDSA over
///      SHA-512, DER-encoded) — this is how every call authenticates, so no call
///      site attaches credentials itself;
///   4. captures the raw response body for the gateway (see <see cref="UpvestRawResponse"/>).
/// The signing recipe follows the uv plugin's csharp-authentication guidance.
/// </summary>
public sealed class UpvestSigningHandler : DelegatingHandler
{
    private static readonly string[] Ignored =
    {
        "cf-", "cdn-", "cookie", "x-", "priority", "upvest-signature", "sec-",
        "user-agent", "accept-encoding", "connection", "host", "expect", "te", "transfer-encoding"
    };

    private readonly ECDsa _key;
    private readonly string _keyId;
    private readonly string _clientId;
    private readonly Uri _baseUrl;

    public UpvestSigningHandler(ECDsa key, string keyId, string clientId, Uri baseUrl)
    {
        _key = key;
        _keyId = keyId;
        _clientId = clientId;
        _baseUrl = baseUrl;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        // Rewrite to the configured host FIRST — the signature covers the path actually sent.
        req.RequestUri = new Uri(_baseUrl, req.RequestUri!.PathAndQuery);

        if (req.Content?.Headers.ContentType is { MediaType: "application/json" } ctype)
            ctype.CharSet = null;

        byte[]? body = req.Content is null ? null : await req.Content.ReadAsByteArrayAsync(ct);

        var accept = req.Headers.Accept.ToString();
        if (accept != "application/json" && accept != "application/pdf")
        {
            req.Headers.Accept.Clear();
            req.Headers.Accept.ParseAdd("application/json");
        }
        if (!req.Headers.Contains("upvest-api-version")) req.Headers.TryAddWithoutValidation("upvest-api-version", "1");
        if (!req.Headers.Contains("upvest-client-id")) req.Headers.TryAddWithoutValidation("upvest-client-id", _clientId);

        // Some SDK write operations (user checks, identifiers) omit the required
        // idempotency-key parameter; supply one here so every write carries it.
        if (req.Method != HttpMethod.Get && req.Method != HttpMethod.Delete && !req.Headers.Contains("idempotency-key"))
            req.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());

        var now = DateTimeOffset.UtcNow;
        req.Headers.Date = now;

        if (body is { Length: > 0 })
        {
            req.Content!.Headers.Remove("content-digest");
            req.Content.Headers.TryAddWithoutValidation(
                "content-digest", "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":");
            req.Content.Headers.ContentLength = body.Length;
        }

        var comps = new List<(string Name, string Value)>
        {
            ("@method", req.Method.Method.ToUpperInvariant()),
            ("@path", string.IsNullOrEmpty(req.RequestUri.AbsolutePath) ? "/" : req.RequestUri.AbsolutePath)
        };
        if (req.RequestUri.Query.Length > 1) comps.Add(("@query", req.RequestUri.Query));

        IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers = req.Headers;
        if (req.Content is not null) headers = headers.Concat(req.Content.Headers);
        foreach (var h in headers)
        {
            var name = h.Key.ToLowerInvariant();
            if (!Ignored.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
                comps.Add((name, string.Join(", ", h.Value)));
        }

        long created = now.ToUnixTimeSeconds();
        string sigParams = "(" + string.Join(" ", comps.Select(c => $"\"{c.Name}\""))
            + $");keyid=\"{_keyId}\";nonce=\"{Guid.NewGuid()}\";created={created};expires={created + 10}";
        var sigBase = string.Join("\n", comps.Select(c => $"\"{c.Name}\": {c.Value}"))
            + "\n\"@signature-params\": " + sigParams;
        byte[] sig = _key.SignData(Encoding.UTF8.GetBytes(sigBase), HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);

        req.Headers.TryAddWithoutValidation("signature-input", "sig1=" + sigParams);
        req.Headers.TryAddWithoutValidation("signature", "sig1=:" + Convert.ToBase64String(sig) + ":");
        req.Headers.TryAddWithoutValidation("upvest-signature-version", "15");

        var response = await base.SendAsync(req, ct);

        // Buffer the body so the gateway can parse it even when the SDK's typed
        // deserialization throws, then re-wrap it so the SDK can still read it.
        if (response.Content is not null)
        {
            byte[] respBytes = await response.Content.ReadAsByteArrayAsync(ct);
            UpvestRawResponse.Record((int)response.StatusCode, Encoding.UTF8.GetString(respBytes));
            var replacement = new ByteArrayContent(respBytes);
            foreach (var h in response.Content.Headers)
                replacement.Headers.TryAddWithoutValidation(h.Key, string.Join(", ", h.Value));
            response.Content = replacement;
        }

        return response;
    }
}
