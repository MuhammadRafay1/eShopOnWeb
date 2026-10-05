using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The single place every Upvest call is authenticated. For each outgoing request it attaches the
/// <c>upvest-client-id</c> header, an OAuth bearer token (except on the token endpoint itself), and an
/// Upvest HTTP message signature over the required components. No call site attaches credentials.
///
/// Signature scheme (Upvest v6 default): ECDSA P-521 / SHA-512, ASN.1 DER, covering <c>@method</c>,
/// <c>@path</c>, <c>upvest-client-id</c>, <c>authorization</c> (off the token endpoint), <c>@query</c> when
/// present, the body triplet (<c>content-length</c>, <c>content-type</c>, <c>digest</c>) when there is a body,
/// and <c>idempotency-key</c> when present. The digest is <c>SHA-256=&lt;base64(sha256(body))&gt;</c>.
/// </summary>
public sealed class UpvestAuthDelegatingHandler : DelegatingHandler
{
    private const string TokenPath = "/auth/token";

    private readonly IUpvestRequestSigner _signer;
    private readonly UpvestSettings _settings;
    private readonly IServiceProvider _services;
    private readonly UpvestResponseCapture _capture;

    public UpvestAuthDelegatingHandler(IUpvestRequestSigner signer, IOptions<UpvestSettings> options, IServiceProvider services, UpvestResponseCapture capture)
    {
        _signer = signer;
        _settings = options.Value;
        _services = services;
        _capture = capture;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("Upvest request has no URI.");
        var path = uri.AbsolutePath;
        var query = uri.Query.TrimStart('?');
        var isTokenEndpoint = string.Equals(path, TokenPath, StringComparison.Ordinal);

        // Buffer the body so the digest and content-length are computed over exactly what is sent.
        byte[] body = Array.Empty<byte>();
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            request.Content.Headers.ContentLength = body.Length;
        }

        // Credential headers owned by this handler (replace any placeholders the SDK set).
        SetRequestHeader(request, "upvest-client-id", _settings.ClientId);

        if (!isTokenEndpoint)
        {
            var tokenProvider = _services.GetRequiredService<IUpvestTokenProvider>();
            var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
            SetRequestHeader(request, "Authorization", "Bearer " + token);
        }

        string? digest = null;
        if (body.Length > 0)
        {
            digest = "SHA-256=" + Convert.ToBase64String(SHA256.HashData(body));
            request.Content!.Headers.Remove("digest");
            request.Content!.Headers.TryAddWithoutValidation("digest", digest);
        }

        // Determine covered components (order is preserved into both signature-input and the base).
        var components = new List<string> { "@method", "@path", "upvest-client-id" };
        if (!isTokenEndpoint) components.Add("authorization");
        if (query.Length > 0) components.Add("@query");
        if (body.Length > 0) components.AddRange(new[] { "content-length", "content-type", "digest" });
        if (GetHeaderValue(request, "idempotency-key") is not null) components.Add("idempotency-key");

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var inner = "(" + string.Join(" ", components.Select(c => $"\"{c}\"")) + ")";
        var paramsValue = $"{inner};created={created.ToString(CultureInfo.InvariantCulture)};keyid=\"{_settings.SigningKeyId}\";alg=\"{_signer.Algorithm}\"";

        var baseLines = new List<string>(components.Count + 1);
        foreach (var c in components)
        {
            var value = c switch
            {
                "@method" => request.Method.Method,
                "@path" => path,
                "@query" => "?" + query,
                "@authority" => uri.Authority.ToLowerInvariant(),
                _ => GetHeaderValue(request, c) ?? string.Empty
            };
            baseLines.Add($"\"{c}\": {value}");
        }
        var signatureBase = string.Join("\n", baseLines) + "\n\"@signature-params\": " + paramsValue;

        var signature = _signer.SignBase(signatureBase);
        SetRequestHeader(request, "signature-input", "sig1=" + paramsValue);
        SetRequestHeader(request, "signature", "sig1=:" + signature + ":");

        // Let transport failures propagate so the SDK pipeline translates them to SdkConnectionException/
        // SdkTimeoutException, which the gateway's error boundary converts into UpvestGatewayException.
        var response = await base.SendAsync(request, cancellationToken);

        // Capture the raw body (buffered so the SDK can still deserialize it) for the gateway to read when
        // the provider returns members the generated models do not expect.
        var holder = _capture.Current;
        if (holder is not null && response.Content is not null)
        {
            await response.Content.LoadIntoBufferAsync();
            holder.Body = await response.Content.ReadAsStringAsync();
            holder.StatusCode = (int)response.StatusCode;
        }

        return response;
    }

    private static void SetRequestHeader(HttpRequestMessage request, string name, string value)
    {
        // These are all request headers (upvest-client-id, Authorization, signature, signature-input);
        // touch only the request header collection.
        request.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, value);
    }

    private static string? GetHeaderValue(HttpRequestMessage request, string name)
    {
        if (request.Headers.TryGetValues(name, out var values))
            return string.Join(", ", values);
        if (request.Content is not null && request.Content.Headers.TryGetValues(name, out var contentValues))
            return string.Join(", ", contentValues);
        return null;
    }
}
