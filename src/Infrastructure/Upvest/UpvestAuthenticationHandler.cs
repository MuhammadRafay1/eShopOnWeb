using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The one and only place that authenticates calls to Upvest. Every request routed through the
/// Upvest <see cref="System.Net.Http.HttpClient"/> passes through here, which:
/// attaches the client-id and API version, obtains and attaches the OAuth bearer token (for every
/// call except the token request itself), and signs the request. No call site attaches credentials.
/// </summary>
public sealed class UpvestAuthenticationHandler : DelegatingHandler
{
    private readonly UpvestTokenProvider _tokenProvider;
    private readonly UpvestMessageSigner _signer;
    private readonly UpvestOptions _options;

    public UpvestAuthenticationHandler(
        UpvestTokenProvider tokenProvider,
        UpvestMessageSigner signer,
        IOptions<UpvestOptions> options)
    {
        _tokenProvider = tokenProvider;
        _signer = signer;
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isTokenRequest = request.RequestUri?.AbsolutePath.EndsWith("/auth/token", StringComparison.Ordinal) == true;

        if (request.Headers.Accept.Count == 0)
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        request.Headers.TryAddWithoutValidation("upvest-client-id", _options.ClientId);
        request.Headers.TryAddWithoutValidation("upvest-api-version", "1");
        // Use v15 of the HTTP message signature scheme (SHA-512 content-digest). Not a signed component.
        request.Headers.TryAddWithoutValidation("upvest-signature-version", "15");

        // Every call except the token request carries the bearer token.
        if (!isTokenRequest)
        {
            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Sign last, once every signed header is in place.
        await _signer.SignAsync(request, cancellationToken);

        return await base.SendAsync(request, cancellationToken);
    }
}
