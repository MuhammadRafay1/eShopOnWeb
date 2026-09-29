using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

public interface IPayPalTokenProvider
{
    /// <summary>Returns a valid OAuth access token, obtaining or refreshing it as needed.</summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
