using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Signs every outgoing Upvest request (adds content-digest, signature-input and signature headers).
/// Placed closest to the network so it signs the final, fully-formed request.
/// </summary>
public sealed class UpvestSigningHandler : DelegatingHandler
{
    private readonly IUpvestRequestSigner _signer;

    public UpvestSigningHandler(IUpvestRequestSigner signer) => _signer = signer;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _signer.SignAsync(request, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Attaches a bearer token scoped to the operation. The caller marks the required scope on the
/// request via the <c>x-upvest-scope</c> header; this handler exchanges it for an access token,
/// removes the marker (so it is never signed or sent), and sets the Authorization header.
/// </summary>
public sealed class UpvestAuthorizationHandler : DelegatingHandler
{
    public const string ScopeHeader = "x-upvest-scope";

    private readonly IUpvestTokenProvider _tokenProvider;

    public UpvestAuthorizationHandler(IUpvestTokenProvider tokenProvider) => _tokenProvider = tokenProvider;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.TryGetValues(ScopeHeader, out var scopes))
        {
            request.Headers.Remove(ScopeHeader);
            var token = await _tokenProvider.GetTokenAsync(string.Join(' ', scopes), cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
