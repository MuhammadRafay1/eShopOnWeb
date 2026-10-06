using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The single reusable handler through which every Upvest call is authenticated. No call site attaches
/// credentials: this handler sets the tenant id and API version, attaches the OAuth2 bearer token (for every
/// call except the token request itself), and computes the HTTP message signature over the final request.
/// </summary>
public sealed class UpvestAuthenticationHandler : DelegatingHandler
{
    private const string TokenPath = "/auth/token";

    private readonly UpvestAccessTokenProvider _tokenProvider;
    private readonly UpvestRequestSigner _signer;
    private readonly UpvestSettings _settings;

    public UpvestAuthenticationHandler(
        UpvestAccessTokenProvider tokenProvider,
        UpvestRequestSigner signer,
        IOptions<UpvestSettings> settings)
    {
        _tokenProvider = tokenProvider;
        _signer = signer;
        _settings = settings.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isTokenRequest = request.RequestUri is not null
            && request.RequestUri.AbsolutePath.EndsWith(TokenPath, StringComparison.Ordinal);

        // Identify the tenant and API version centrally (replacing any placeholder set at the call site).
        SetHeader(request, "upvest-client-id", _settings.ClientId);
        if (!request.Headers.Contains("upvest-api-version"))
        {
            request.Headers.TryAddWithoutValidation("upvest-api-version", "1");
        }

        // Use the v15 signature scheme (content-digest / SHA-512) that the signer produces.
        SetHeader(request, "upvest-signature-version", "15");

        if (isTokenRequest)
        {
            // The token request authenticates by signature + client credentials in the body, never a bearer.
            request.Headers.Remove("Authorization");
        }
        else
        {
            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
            SetHeader(request, "Authorization", $"Bearer {token}");
        }

        var body = request.Content is null
            ? Array.Empty<byte>()
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        _signer.Sign(request, body);

        var response = await base.SendAsync(request, cancellationToken);

        // On a 401 for a non-token call, drop the cached token so the next attempt re-acquires one.
        if (response.StatusCode == HttpStatusCode.Unauthorized && !isTokenRequest)
        {
            _tokenProvider.Invalidate();
        }

        return response;
    }

    private static void SetHeader(HttpRequestMessage request, string name, string value)
    {
        request.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, value);
    }
}
