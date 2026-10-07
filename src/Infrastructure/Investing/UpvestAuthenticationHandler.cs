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
/// The single, reusable pipeline stage through which every call this application makes to Upvest
/// passes. It authenticates each call by attaching an HTTP message signature (the credential Upvest
/// enforces on every request, the OAuth token request included) and pins the request to the configured
/// Upvest base URL. No call site attaches credentials itself; the OAuth bearer token is acquired by the
/// SDK's auth manager, whose own token request travels through this handler and is signed here too.
/// <para>
/// The signing scheme reproduces exactly what the Upvest SDK's internal signer does (RFC 9421 with
/// ECDSA over SHA-512, DER-encoded), so it is accepted by Upvest unchanged.
/// </para>
/// </summary>
public sealed class UpvestAuthenticationHandler : DelegatingHandler
{
    // Header name prefixes the signature never covers (per the SDK's signer).
    private static readonly string[] IgnoredHeaderPrefixes =
    {
        "cf-", "cdn-", "cookie", "x-", "priority", "upvest-signature", "sec-",
        "user-agent", "accept-encoding", "connection", "host", "expect", "te", "transfer-encoding"
    };

    private readonly ECDsa _key;
    private readonly object _signLock = new();
    private readonly string _keyId;
    private readonly string _clientId;
    private readonly Uri _baseUrl;

    public UpvestAuthenticationHandler(ECDsa key, string keyId, string clientId, Uri baseUrl)
    {
        _key = key;
        _keyId = keyId;
        _clientId = clientId;
        _baseUrl = baseUrl;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Pin to the configured base URL first — the signature covers the path that is actually sent.
        request.RequestUri = new Uri(_baseUrl, request.RequestUri!.PathAndQuery);

        if (request.Content?.Headers.ContentType is { MediaType: "application/json" } contentType)
        {
            contentType.CharSet = null;
        }

        byte[]? body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        var accept = request.Headers.Accept.ToString();
        if (accept != "application/json" && accept != "application/pdf")
        {
            request.Headers.Accept.Clear();
            request.Headers.Accept.ParseAdd("application/json");
        }

        if (!request.Headers.Contains("upvest-api-version"))
        {
            request.Headers.TryAddWithoutValidation("upvest-api-version", "1");
        }

        if (!request.Headers.Contains("upvest-client-id"))
        {
            request.Headers.TryAddWithoutValidation("upvest-client-id", _clientId);
        }

        // Upvest requires a unique idempotency-key on every mutating request. Some SDK operations do not
        // expose it as a parameter, so the one central handler supplies it when a call site has not. The
        // token request is exempt. A fresh key per send is correct here: the SDK performs no retries.
        var method = request.Method;
        if ((method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch)
            && !request.Headers.Contains("idempotency-key")
            && !request.RequestUri.AbsolutePath.StartsWith("/auth", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());
        }

        var now = DateTimeOffset.UtcNow;
        request.Headers.Date = now;

        if (body is { Length: > 0 })
        {
            request.Content!.Headers.Remove("content-digest");
            request.Content.Headers.TryAddWithoutValidation(
                "content-digest", "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":");
            request.Content.Headers.ContentLength = body.Length;
        }

        var components = new List<(string Name, string Value)>
        {
            ("@method", request.Method.Method.ToUpperInvariant()),
            ("@path", string.IsNullOrEmpty(request.RequestUri.AbsolutePath) ? "/" : request.RequestUri.AbsolutePath)
        };
        if (request.RequestUri.Query.Length > 1)
        {
            components.Add(("@query", request.RequestUri.Query));
        }

        IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers = request.Headers;
        if (request.Content is not null)
        {
            headers = headers.Concat(request.Content.Headers);
        }

        foreach (var header in headers)
        {
            var name = header.Key.ToLowerInvariant();
            if (!IgnoredHeaderPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            {
                components.Add((name, string.Join(", ", header.Value)));
            }
        }

        long created = now.ToUnixTimeSeconds();
        string signatureParams = "(" + string.Join(" ", components.Select(c => $"\"{c.Name}\""))
            + $");keyid=\"{_keyId}\";nonce=\"{Guid.NewGuid()}\";created={created};expires={created + 10}";
        string signatureBase = string.Join("\n", components.Select(c => $"\"{c.Name}\": {c.Value}"))
            + "\n\"@signature-params\": " + signatureParams;

        byte[] signature;
        lock (_signLock)
        {
            signature = _key.SignData(
                Encoding.UTF8.GetBytes(signatureBase), HashAlgorithmName.SHA512, DSASignatureFormat.Rfc3279DerSequence);
        }

        request.Headers.TryAddWithoutValidation("signature-input", "sig1=" + signatureParams);
        request.Headers.TryAddWithoutValidation("signature", "sig1=:" + Convert.ToBase64String(signature) + ":");
        request.Headers.TryAddWithoutValidation("upvest-signature-version", "15");

        return await base.SendAsync(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        // The key is owned by DI (registered as a singleton alongside this handler); do not dispose it here.
        base.Dispose(disposing);
    }
}
