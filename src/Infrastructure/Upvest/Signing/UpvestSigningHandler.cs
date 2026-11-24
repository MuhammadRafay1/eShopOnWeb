using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest.Signing;

/// <summary>
/// Delegating handler that signs every outgoing Upvest request. Registered as the
/// innermost handler so it signs the final request (including any Authorization header).
/// </summary>
public sealed class UpvestSigningHandler : DelegatingHandler
{
    private readonly UpvestRequestSigner _signer;

    public UpvestSigningHandler(UpvestRequestSigner signer)
    {
        _signer = signer;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _signer.SignAsync(request, cancellationToken).ConfigureAwait(false);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
