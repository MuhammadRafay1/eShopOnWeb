using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>Supplies a valid OAuth bearer token for Upvest, acquiring and caching it as needed.</summary>
public interface IUpvestTokenProvider
{
    /// <summary>Returns a valid access token value (without the "Bearer " prefix).</summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
