using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Investing.Http;

/// <summary>Supplies a valid OAuth2 access token for the Upvest Investment API, refreshing as needed.</summary>
public interface IUpvestTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Discard the cached token so the next call fetches a fresh one (e.g. after a 401).</summary>
    void Invalidate();
}
