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
/// The single, reusable <see cref="DelegatingHandler"/> through which every call this application
/// makes to Upvest passes. It authenticates each call by attaching the HTTP message signature
/// Upvest requires on every request (including the OAuth token request, which flows through the
/// same handler). It also rewrites each request onto the configured <c>Upvest:BaseUrl</c>, so no
/// call site deals with the host or with credentials.
/// </summary>
/// <remarks>
/// The signing scheme (covered components, Upvest headers, ECDSA-over-SHA-512 in DER, version 15)
/// mirrors the Upvest SDK's own signer exactly.
/// </remarks>
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
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _keyId = keyId ?? throw new ArgumentNullException(nameof(keyId));
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        // Rewrite onto the configured base URL first; the signature covers the path actually sent.
        req.RequestUri = new Uri(_baseUrl, req.RequestUri!.PathAndQuery);

        if (req.Content?.Headers.ContentType is { MediaType: "application/json" } ctype)
        {
            ctype.CharSet = null;
        }

        byte[]? body = req.Content is null ? null : await req.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

        // Upvest requires an idempotency-key on write requests. The SDK sets it for most write
        // operations but omits the parameter on a few (e.g. creating a user check or a webhook),
        // so fill it in here when missing. Operations that supply their own key keep it.
        if (req.Method == HttpMethod.Post && !req.Headers.Contains("idempotency-key"))
        {
            req.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());
        }

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

        if (!req.Headers.Contains("upvest-client-id"))
        {
            req.Headers.TryAddWithoutValidation("upvest-client-id", _clientId);
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
        if (req.RequestUri.Query.Length > 1)
        {
            comps.Add(("@query", req.RequestUri.Query));
        }

        IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers = req.Headers;
        if (req.Content is not null)
        {
            headers = headers.Concat(req.Content.Headers);
        }

        foreach (var h in headers)
        {
            var name = h.Key.ToLowerInvariant();
            if (!Ignored.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            {
                comps.Add((name, string.Join(", ", h.Value)));
            }
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

        return await base.SendAsync(req, ct).ConfigureAwait(false);
    }
}
