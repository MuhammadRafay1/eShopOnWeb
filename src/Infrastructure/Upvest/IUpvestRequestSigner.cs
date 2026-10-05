using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Signs an outgoing Upvest request in place: buffers the body, computes the content digest, and sets the
/// <c>signature-input</c> and <c>signature</c> headers (and <c>digest</c> for bodied requests) covering the
/// components Upvest requires. The caller (the one DelegatingHandler) has already set <c>upvest-client-id</c>
/// and, for non-token requests, <c>Authorization</c>.
/// </summary>
public interface IUpvestRequestSigner
{
    Task SignAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}
