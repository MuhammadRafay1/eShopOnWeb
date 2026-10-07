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
/// The one reusable handler through which every Upvest call passes. It authenticates each request
/// by attaching an HTTP message signature (version 15, the form Upvest accepts), and rewrites the
/// request onto the configured base URL. No call site attaches credentials itself.
///
/// The algorithm mirrors the vendored SDK's own signer byte-for-byte: the SDK's signer only ever
/// targets its built-in environment hosts and cannot be pointed at the configured base URL, so the
/// signing is reproduced here instead.
/// </summary>
public sealed class UpvestSigningHandler : DelegatingHandler
{
    private static readonly string[] IgnoredPrefixes =
    {
        "cf-", "cdn-", "cookie", "x-", "priority", "upvest-signature", "sec-",
        "user-agent", "accept-encoding", "connection", "host", "expect", "te", "transfer-encoding"
    };

    private readonly ECDsa _key;
    private readonly string _keyId;
    private readonly string _clientId;
    private readonly Uri _baseUrl;
    private readonly object _signLock = new();

    public UpvestSigningHandler(ECDsa key, string keyId, string clientId, Uri baseUrl, HttpMessageHandler? innerHandler = null)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _keyId = keyId ?? throw new ArgumentNullException(nameof(keyId));
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        InnerHandler = innerHandler ?? new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        // Rewrite onto the configured base URL FIRST — the signature covers the path actually sent.
        req.RequestUri = new Uri(_baseUrl, req.RequestUri!.PathAndQuery);

        if (req.Content?.Headers.ContentType is { MediaType: "application/json" } ctype)
        {
            ctype.CharSet = null;
        }

        byte[]? body = req.Content is null ? null : await req.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

        var accept = req.Headers.Accept.ToString();
        if (accept != "application/json" && accept != "application/pdf")
        {
            req.Headers.Accept.Clear();
            req.Headers.Accept.ParseAdd("application/json");
        }

        if (!req.Headers.Contains("upvest-api-version")) req.Headers.TryAddWithoutValidation("upvest-api-version", "1");
        if (!req.Headers.Contains("upvest-client-id")) req.Headers.TryAddWithoutValidation("upvest-client-id", _clientId);

        // Upvest requires an idempotency-key on mutating (POST) calls. The vendored SDK sends it for
        // some operations but omits it on others (e.g. create-user-check, create-webhook), where the
        // request would otherwise be rejected 400. Supply a fallback here, centrally, for any POST that
        // does not already carry one — except the token request, which is not an idempotent mutation.
        var path = req.RequestUri.AbsolutePath;
        var isTokenRequest = path.TrimEnd('/').EndsWith("/auth/token", StringComparison.OrdinalIgnoreCase);
        if (req.Method == HttpMethod.Post && !isTokenRequest && !req.Headers.Contains("idempotency-key"))
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
            if (!IgnoredPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            {
                comps.Add((name, string.Join(", ", h.Value)));
            }
        }

        long created = now.ToUnixTimeSeconds();
        string sigParams = "(" + string.Join(" ", comps.Select(c => $"\"{c.Name}\""))
            + $");keyid=\"{_keyId}\";nonce=\"{Guid.NewGuid()}\";created={created};expires={created + 10}";
        var sigBase = string.Join("\n", comps.Select(c => $"\"{c.Name}\": {c.Value}"))
            + "\n\"@signature-params\": " + sigParams;

        byte[] sig;
        lock (_signLock)
        {
            sig = _key.SignData(Encoding.UTF8.GetBytes(sigBase), HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        }

        req.Headers.TryAddWithoutValidation("signature-input", "sig1=" + sigParams);
        req.Headers.TryAddWithoutValidation("signature", "sig1=:" + Convert.ToBase64String(sig) + ":");
        req.Headers.TryAddWithoutValidation("upvest-signature-version", "15");

        return await base.SendAsync(req, ct).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _key.Dispose();
        base.Dispose(disposing);
    }
}
