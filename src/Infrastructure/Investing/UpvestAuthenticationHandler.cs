using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// The one reusable <see cref="DelegatingHandler"/> through which every call this application makes to Upvest
/// passes. It authenticates each call: it attaches the OAuth2 bearer token (except on the token endpoint
/// itself, which has no bearer yet) and the HTTP message signature. No call site attaches credentials.
/// It also captures the raw response so the gateway can read bodies the SDK's strict models reject.
/// </summary>
public sealed class UpvestAuthenticationHandler : DelegatingHandler
{
    public const string HttpClientName = "upvest";

    private readonly UpvestTokenProvider _tokenProvider;
    private readonly UpvestRequestSigner _signer;
    private readonly UpvestResponseCapture _capture;
    private readonly UpvestOptions _options;

    public UpvestAuthenticationHandler(
        UpvestTokenProvider tokenProvider,
        UpvestRequestSigner signer,
        UpvestResponseCapture capture,
        UpvestOptions options)
    {
        _tokenProvider = tokenProvider;
        _signer = signer;
        _capture = capture;
        _options = options;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isTokenEndpoint = request.RequestUri is not null &&
                              request.RequestUri.AbsolutePath.Equals("/auth/token", StringComparison.OrdinalIgnoreCase);

        if (!isTokenEndpoint)
        {
            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
            SetHeader(request, "authorization", "Bearer " + token);
        }

        // The tenant id and API version are not secrets; the handler guarantees they are present and correct
        // so that no call site has to set them.
        SetHeader(request, "upvest-client-id", _options.ClientId);
        SetHeader(request, "upvest-api-version", "1");

        await _signer.SignAsync(request, cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (!isTokenEndpoint)
        {
            // Buffer so the SDK can still read the body, and capture it for the gateway's defensive parsing.
            await response.Content.LoadIntoBufferAsync();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _capture.Record((int)response.StatusCode, body);
        }

        return response;
    }

    private static void SetHeader(HttpRequestMessage request, string name, string value)
    {
        request.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, value);
    }
}
