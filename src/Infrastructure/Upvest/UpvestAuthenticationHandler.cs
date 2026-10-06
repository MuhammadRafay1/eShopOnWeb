using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The one reusable delegating handler through which every Upvest API call passes — including the
/// access-token request. It applies the v15 HTTP message signature to every request and, for all
/// calls except the token request itself, attaches the OAuth bearer token. No call site attaches
/// credentials itself.
/// </summary>
public sealed class UpvestAuthenticationHandler : DelegatingHandler
{
    private readonly IUpvestTokenProvider _tokenProvider;
    private readonly IUpvestRequestSigner _signer;

    public UpvestAuthenticationHandler(IUpvestTokenProvider tokenProvider, IUpvestRequestSigner signer)
    {
        _tokenProvider = tokenProvider;
        _signer = signer;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // The access-token request is signed but carries no bearer (there is no token yet), and must
        // not trigger a token fetch — doing so would recurse.
        var isTokenRequest = request.RequestUri is not null &&
            request.RequestUri.AbsolutePath.EndsWith("/auth/token", StringComparison.OrdinalIgnoreCase);

        var token = isTokenRequest ? null : await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        await _signer.SignAsync(request, token, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
