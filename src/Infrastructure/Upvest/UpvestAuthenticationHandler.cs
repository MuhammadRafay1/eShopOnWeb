using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The single reusable authentication handler for every call this application makes to Upvest. No call site
/// attaches credentials: this handler sets the <c>upvest-client-id</c> header, attaches the OAuth bearer
/// token (on every call except the token endpoint itself), and signs the request. It logs only the method,
/// the path (never the query), the status and the provider request-id — never a body, header value or
/// personal detail.
/// </summary>
public sealed class UpvestAuthenticationHandler : DelegatingHandler
{
    private const string TokenPath = "/auth/token";

    private readonly IUpvestRequestSigner _signer;
    private readonly IUpvestTokenProvider _tokenProvider;
    private readonly UpvestOptions _options;
    private readonly ILogger<UpvestAuthenticationHandler> _logger;

    public UpvestAuthenticationHandler(
        IUpvestRequestSigner signer,
        IUpvestTokenProvider tokenProvider,
        IOptions<UpvestOptions> options,
        ILogger<UpvestAuthenticationHandler> logger)
    {
        _signer = signer;
        _tokenProvider = tokenProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var isToken = string.Equals(path, TokenPath, StringComparison.Ordinal);

        // 1. Identify the client on every call.
        request.Headers.Remove("upvest-client-id");
        request.Headers.TryAddWithoutValidation("upvest-client-id", _options.ClientId);

        // 2. Attach the bearer on every call but the token acquisition itself.
        if (!isToken)
        {
            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // 3. Sign the request (digest + signature-input + signature).
        await _signer.SignAsync(request, cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);

        var requestId = response.Headers.TryGetValues("upvest-request-id", out var ids)
            ? string.Join(",", ids) : "-";
        if (response.IsSuccessStatusCode)
            _logger.LogDebug("Upvest {Method} {Path} -> {Status} (request-id {RequestId})", request.Method.Method, path, (int)response.StatusCode, requestId);
        else
            _logger.LogWarning("Upvest {Method} {Path} -> {Status} (request-id {RequestId})", request.Method.Method, path, (int)response.StatusCode, requestId);

        return response;
    }
}
