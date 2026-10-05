using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The single place every Upvest call is authenticated. For each outgoing request it attaches the
/// <c>upvest-client-id</c> header, acquires and attaches the OAuth bearer (except on the token endpoint
/// itself), computes the body digest, and signs the request with the Upvest HTTP message signature. No call
/// site attaches any credential. It also captures the raw response body (for the gateway) and logs the
/// provider's request id — never request bodies, which carry personal data.
/// </summary>
public sealed class UpvestSigningHandler : DelegatingHandler
{
    private readonly IUpvestRequestSigner _signer;
    private readonly IUpvestTokenProvider _tokenProvider;
    private readonly UpvestResponseCapture _capture;
    private readonly ILogger<UpvestSigningHandler> _logger;

    public UpvestSigningHandler(
        IUpvestRequestSigner signer,
        IUpvestTokenProvider tokenProvider,
        UpvestResponseCapture capture,
        ILogger<UpvestSigningHandler> logger)
    {
        _signer = signer;
        _tokenProvider = tokenProvider;
        _capture = capture;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("Upvest request has no URI.");
        var path = uri.AbsolutePath;
        var query = uri.Query.TrimStart('?');
        var isTokenEndpoint = path.EndsWith("/auth/token", StringComparison.Ordinal);

        byte[] body = Array.Empty<byte>();
        string? contentType = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            contentType = request.Content.Headers.ContentType?.ToString();
            request.Content.Headers.ContentLength = body.Length;
        }

        // upvest-client-id — set authoritatively by the handler.
        request.Headers.Remove("upvest-client-id");
        request.Headers.TryAddWithoutValidation("upvest-client-id", _signer.ClientId);

        // Bearer token for everything except the token endpoint (which has no bearer requirement).
        string? authorization = null;
        if (!isTokenEndpoint)
        {
            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            authorization = request.Headers.Authorization.ToString();
        }

        string? idempotencyKey = null;
        if (request.Headers.TryGetValues("idempotency-key", out var idem))
            idempotencyKey = idem.FirstOrDefault();

        var sig = _signer.Sign(
            request.Method.Method, path, string.IsNullOrEmpty(query) ? null : query, body, contentType, authorization, idempotencyKey);

        request.Headers.Remove("signature-input");
        request.Headers.Remove("signature");
        request.Headers.TryAddWithoutValidation("signature-input", sig.SignatureInput);
        request.Headers.TryAddWithoutValidation("signature", sig.Signature);
        if (sig.Digest is not null && request.Content is not null)
        {
            request.Content.Headers.Remove("digest");
            request.Content.Headers.TryAddWithoutValidation("digest", sig.Digest);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.Content is not null)
        {
            await response.Content.LoadIntoBufferAsync();
            _capture.Record(await response.Content.ReadAsStringAsync(cancellationToken), (int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode &&
            response.Headers.TryGetValues("upvest-request-id", out var ids))
        {
            _logger.LogWarning("Upvest {Method} {Path} -> {Status} (upvest-request-id: {RequestId})",
                request.Method.Method, path, (int)response.StatusCode, ids.FirstOrDefault());
        }

        return response;
    }
}
