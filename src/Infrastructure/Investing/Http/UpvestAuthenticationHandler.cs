using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Investing.Http;

/// <summary>
/// The one reusable handler that authenticates every call this application makes to Upvest.
/// No call site attaches credentials itself — this handler:
///   1. attaches the OAuth2 bearer token (fetched/cached by <see cref="IUpvestTokenProvider"/>),
///   2. adds the common Upvest headers (client id, api version, signature version, idempotency key), and
///   3. signs the request with an HTTP message signature (v15): ECDSA P-521 / SHA-512 over a
///      signature base of the method, path, query and the covered headers, plus a SHA-512
///      content-digest of the body.
/// The token endpoint is special-cased: it is signed like everything else but is not given a
/// bearer token (it is how the bearer token is obtained), which keeps token acquisition from recursing.
/// </summary>
public sealed class UpvestAuthenticationHandler : DelegatingHandler
{
    // Covered components, in the order Upvest's implementation guide lists them. Only those
    // actually present on the request are included (e.g. GET requests carry no body, so the
    // content-* components are omitted).
    private const string MethodComponent = "@method";
    private const string PathComponent = "@path";
    private const string QueryComponent = "@query";

    private readonly IUpvestTokenProvider _tokenProvider;
    private readonly IUpvestMessageSigner _signer;
    private readonly UpvestSettings _settings;

    public UpvestAuthenticationHandler(
        IUpvestTokenProvider tokenProvider,
        IUpvestMessageSigner signer,
        IOptions<UpvestSettings> options)
    {
        _tokenProvider = tokenProvider;
        _signer = signer;
        _settings = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await SignAndSendAsync(request, cancellationToken);

        // If the token was rejected, drop the cached token and retry once with a fresh one.
        if (response.StatusCode == HttpStatusCode.Unauthorized && !IsTokenRequest(request))
        {
            _tokenProvider.Invalidate();
            response.Dispose();
            response = await SignAndSendAsync(await CloneAsync(request, cancellationToken), cancellationToken);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SignAndSendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isTokenRequest = IsTokenRequest(request);
        var method = request.Method.Method.ToUpperInvariant();

        // Common headers.
        SetHeader(request, "upvest-client-id", _settings.ClientId);
        SetHeader(request, "upvest-api-version", UpvestConstants.ApiVersion);
        SetHeader(request, "upvest-signature-version", UpvestConstants.SignatureVersion);
        SetHeader(request, "Accept", "application/json");

        string? authorization = null;
        if (!isTokenRequest)
        {
            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            authorization = $"Bearer {token}";
        }

        string? idempotencyKey = null;
        if (!isTokenRequest && IsWrite(request.Method))
        {
            idempotencyKey = Guid.NewGuid().ToString();
            SetHeader(request, "idempotency-key", idempotencyKey);
        }

        // content-digest and content metadata (only when there is a body).
        string? contentDigest = null;
        string? contentType = null;
        long? contentLength = null;
        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            contentLength = body.Length;
            contentType = request.Content.Headers.ContentType?.ToString();
            contentDigest = "sha-512=:" + Convert.ToBase64String(SHA512.HashData(body)) + ":";
            request.Content.Headers.TryAddWithoutValidation("content-digest", contentDigest);
        }

        var uri = request.RequestUri ?? throw new InvalidOperationException("Upvest request has no URI.");
        var query = string.IsNullOrEmpty(uri.Query) ? "?" : uri.Query;

        // Build the ordered list of covered components and their values.
        var components = new List<(string Name, string Value)>
        {
            (MethodComponent, method),
            (PathComponent, uri.AbsolutePath),
            (QueryComponent, query),
            ("accept", "application/json"),
        };
        if (authorization is not null)
        {
            components.Add(("authorization", authorization));
        }
        if (contentLength is not null)
        {
            components.Add(("content-length", contentLength.Value.ToString()));
        }
        if (contentType is not null)
        {
            components.Add(("content-type", contentType));
        }
        if (contentDigest is not null)
        {
            components.Add(("content-digest", contentDigest));
        }
        if (idempotencyKey is not null)
        {
            components.Add(("idempotency-key", idempotencyKey));
        }
        components.Add(("upvest-client-id", _settings.ClientId));

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));

        var componentList = string.Join(" ", components.Select(c => $"\"{c.Name}\""));
        var signatureParams = $"({componentList});keyid=\"{_signer.KeyId}\";created={created};nonce=\"{nonce}\"";

        var baseBuilder = new StringBuilder();
        foreach (var (name, value) in components)
        {
            baseBuilder.Append('"').Append(name).Append("\": ").Append(value).Append('\n');
        }
        baseBuilder.Append("\"@signature-params\": ").Append(signatureParams);

        var signature = _signer.SignToBase64(baseBuilder.ToString());

        SetHeader(request, "signature-input", $"sig1={signatureParams}");
        SetHeader(request, "signature", $"sig1=:{signature}:");

        return await base.SendAsync(request, cancellationToken);
    }

    private static bool IsTokenRequest(HttpRequestMessage request) =>
        request.RequestUri is not null && request.RequestUri.AbsolutePath.EndsWith("/auth/token", StringComparison.Ordinal);

    private static bool IsWrite(HttpMethod method) =>
        method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch || method == HttpMethod.Delete;

    private static void SetHeader(HttpRequestMessage request, string name, string value)
    {
        request.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, value);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var newContent = new ByteArrayContent(bytes);
            if (request.Content.Headers.ContentType is not null)
            {
                newContent.Headers.ContentType = request.Content.Headers.ContentType;
            }
            clone.Content = newContent;
        }
        clone.Version = request.Version;
        return clone;
    }
}
