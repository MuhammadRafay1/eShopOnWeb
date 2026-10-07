using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The single, reusable chokepoint through which every call this application makes to
/// Upvest passes (the OAuth token request included). It authenticates each call by applying
/// the HTTP message signature Upvest requires on every request — no call site attaches any
/// credential itself.
///
/// It also points the SDK at the configured base URL: the SDK has no base-URL setter, so the
/// request URI is rewritten here before signing, which keeps the signed path identical to the
/// path that goes on the wire. The signing reproduces exactly what the SDK's own signer does
/// (RFC 9421 draft 15, ECDSA over SHA-512, DER-encoded), so it is accepted verbatim by Upvest.
///
/// The OAuth bearer token is obtained and refreshed by the SDK's client-credentials manager;
/// because the token request travels through this same handler, it is signed too.
/// </summary>
public sealed class UpvestAuthenticationHandler : DelegatingHandler
{
    // Headers excluded from the signature: added by intermediaries or explicitly ignored by Upvest.
    private static readonly string[] IgnorableHeaderPrefixes =
    {
        "cf-", "cdn-", "cookie", "x-", "priority", "upvest-signature", "sec-",
        "user-agent", "accept-encoding", "connection", "host", "expect", "te", "transfer-encoding"
    };

    private static readonly string[] AcceptableAcceptValues = { "application/json", "application/pdf" };

    private const string SignatureVersion = "15";
    private const string ApiVersion = "1";
    private static readonly TimeSpan SignatureLifetime = TimeSpan.FromSeconds(10);

    private readonly ECDsa _key;
    private readonly string _keyId;
    private readonly string _clientId;
    private readonly Uri _baseUrl;

    public UpvestAuthenticationHandler(ECDsa key, string keyId, string clientId, Uri baseUrl)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _keyId = string.IsNullOrEmpty(keyId) ? throw new ArgumentException("A signing key id is required.", nameof(keyId)) : keyId;
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Send to the configured base URL, keeping the path and query the SDK built.
        var pathAndQuery = request.RequestUri is null ? "/" : request.RequestUri.PathAndQuery;
        request.RequestUri = new Uri(_baseUrl, pathAndQuery);

        // Upvest rejects "application/json; charset=utf-8"; sign and send a bare content type.
        if (request.Content?.Headers.ContentType is { MediaType: "application/json" } contentType
            && !string.IsNullOrEmpty(contentType.CharSet))
        {
            contentType.CharSet = null;
        }

        byte[]? body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        // Collect the headers already on the request (and content), dropping ignorable ones,
        // lower-cased, later value winning — the order servers fold repeated headers in.
        var covered = new List<KeyValuePair<string, string>>();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        void Cover(string name, string value)
        {
            var lower = name.ToLowerInvariant();
            if (index.TryGetValue(lower, out var at))
            {
                covered[at] = new KeyValuePair<string, string>(lower, value);
            }
            else
            {
                index[lower] = covered.Count;
                covered.Add(new KeyValuePair<string, string>(lower, value));
            }
        }

        foreach (var header in EnumerateHeaders(request))
        {
            if (IsIgnorable(header.Key)) continue;
            Cover(header.Key, header.Value);
        }

        // Headers Upvest expects, added only when absent, and then put on the wire too.
        var toAdd = new List<KeyValuePair<string, string>>();
        void Add(string name, string value)
        {
            toAdd.Add(new KeyValuePair<string, string>(name, value));
            Cover(name, value);
        }

        if (!covered.Any(h => h.Key == "accept") || !AcceptableAcceptValues.Contains(covered.First(h => h.Key == "accept").Value))
        {
            Add("accept", "application/json");
        }

        if (!index.ContainsKey("upvest-api-version"))
        {
            Add("upvest-api-version", ApiVersion);
        }

        if (!string.IsNullOrEmpty(_clientId) && !index.ContainsKey("upvest-client-id"))
        {
            Add("upvest-client-id", _clientId);
        }

        // Upvest requires a unique idempotency-key on every mutating request (except the token
        // request). Some SDK operations do not send one; add it centrally so no call site has to,
        // and so it is covered by the signature exactly as the operations that do send it.
        if (IsMutating(request.Method)
            && !request.RequestUri.AbsolutePath.EndsWith("/auth/token", StringComparison.Ordinal)
            && !index.ContainsKey("idempotency-key"))
        {
            Add("idempotency-key", Guid.NewGuid().ToString());
        }

        var now = DateTimeOffset.UtcNow;
        Add("date", now.ToString("r", CultureInfo.InvariantCulture));

        if (body is { Length: > 0 })
        {
            var digest = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":";
            Add("content-digest", digest);
            Add("content-length", body.Length.ToString(CultureInfo.InvariantCulture));
        }

        // Derived components: method, path, and query only when there is one.
        var components = new List<KeyValuePair<string, string>>
        {
            new("@method", request.Method.Method.ToUpperInvariant()),
            new("@path", string.IsNullOrEmpty(request.RequestUri.AbsolutePath) ? "/" : request.RequestUri.AbsolutePath)
        };
        if (request.RequestUri.Query.Length > 1)
        {
            components.Add(new KeyValuePair<string, string>("@query", request.RequestUri.Query));
        }
        components.AddRange(covered);

        long created = now.ToUnixTimeSeconds();
        long expires = now.Add(SignatureLifetime).ToUnixTimeSeconds();
        var signatureParams = BuildSignatureParams(components, created, expires);

        var signatureBase = new StringBuilder();
        foreach (var component in components)
        {
            signatureBase.Append('"').Append(component.Key).Append("\": ").Append(component.Value).Append('\n');
        }
        signatureBase.Append("\"@signature-params\": ").Append(signatureParams);

        byte[] signature = _key.SignData(
            Encoding.UTF8.GetBytes(signatureBase.ToString()),
            HashAlgorithmName.SHA512,
            DSASignatureFormat.Rfc3279DerSequence);

        // Put the added headers, then the signature headers, on the request.
        foreach (var header in toAdd)
        {
            ApplyHeader(request, header.Key, header.Value);
        }

        ApplyHeader(request, "signature-input", "sig1=" + signatureParams);
        ApplyHeader(request, "signature", "sig1=:" + Convert.ToBase64String(signature) + ":");
        // Never covered by the signature, so it goes on last.
        ApplyHeader(request, "upvest-signature-version", SignatureVersion);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (System.Environment.GetEnvironmentVariable("UPVEST_DEBUG_RESPONSE") == "1"
            && request.RequestUri!.AbsolutePath.Contains("/checks", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"[upvest-checks] {(int)response.StatusCode} {request.Method.Method} {request.RequestUri.AbsolutePath}");
        }

        if (!response.IsSuccessStatusCode
            && System.Environment.GetEnvironmentVariable("UPVEST_DEBUG_RESPONSE") == "1"
            && response.Content is not null)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Console.Error.WriteLine($"[upvest] {(int)response.StatusCode} {request.Method.Method} {request.RequestUri!.AbsolutePath} -> {errorBody[..Math.Min(500, errorBody.Length)]}");
            var buffered = new System.Net.Http.StringContent(errorBody);
            foreach (var h in response.Content.Headers)
            {
                buffered.Headers.Remove(h.Key);
                buffered.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
            response.Content = buffered;
        }

        return response;
    }

    private static IEnumerable<KeyValuePair<string, string>> EnumerateHeaders(HttpRequestMessage request)
    {
        foreach (var header in request.Headers)
        {
            yield return new KeyValuePair<string, string>(header.Key, string.Join(", ", header.Value));
        }

        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                yield return new KeyValuePair<string, string>(header.Key, string.Join(", ", header.Value));
            }
        }
    }

    private static bool IsMutating(HttpMethod method)
        => method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch || method == HttpMethod.Delete;

    private static bool IsIgnorable(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        var lower = name.ToLowerInvariant();
        return IgnorableHeaderPrefixes.Any(prefix => lower.StartsWith(prefix, StringComparison.Ordinal));
    }

    private string BuildSignatureParams(IList<KeyValuePair<string, string>> components, long created, long expires)
    {
        var keys = new StringBuilder();
        for (var i = 0; i < components.Count; i++)
        {
            if (i > 0) keys.Append(' ');
            keys.Append('"').Append(components[i].Key).Append('"');
        }

        return "(" + keys + ")"
            + ";keyid=\"" + _keyId + "\""
            + ";nonce=\"" + Guid.NewGuid() + "\""
            + ";created=" + created.ToString(CultureInfo.InvariantCulture)
            + ";expires=" + expires.ToString(CultureInfo.InvariantCulture);
    }

    private static void ApplyHeader(HttpRequestMessage request, string name, string value)
    {
        if (string.Equals(name, "content-length", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Content is not null && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length))
            {
                request.Content.Headers.ContentLength = length;
            }
            return;
        }

        request.Headers.Remove(name);
        if (request.Headers.TryAddWithoutValidation(name, value))
        {
            return;
        }

        if (request.Content is not null)
        {
            request.Content.Headers.Remove(name);
            request.Content.Headers.TryAddWithoutValidation(name, value);
        }
    }

    protected override void Dispose(bool disposing)
    {
        // The ECDsa key lives for the lifetime of the (singleton) client; do not dispose it here.
        base.Dispose(disposing);
    }
}
