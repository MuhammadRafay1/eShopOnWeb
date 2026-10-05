using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The single, reusable <see cref="DelegatingHandler"/> through which every call this application
/// makes to Upvest is authenticated: it attaches the OAuth2 bearer token and signs the request
/// with HTTP Message Signatures. No call site attaches credentials itself.
/// </summary>
public sealed class UpvestSigningHandler : DelegatingHandler
{
    private readonly IUpvestTokenProvider _tokenProvider;
    private readonly UpvestRequestSigner _signer;

    public UpvestSigningHandler(IUpvestTokenProvider tokenProvider, UpvestRequestSigner signer)
    {
        _tokenProvider = tokenProvider;
        _signer = signer;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isTokenRequest = request.RequestUri?.AbsolutePath.EndsWith("/auth/token", StringComparison.Ordinal) == true;

        if (!isTokenRequest)
        {
            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Sign last, so the authorization header is covered by the signature.
        await _signer.SignAsync(request, cancellationToken);

        return await base.SendAsync(request, cancellationToken);
    }
}
