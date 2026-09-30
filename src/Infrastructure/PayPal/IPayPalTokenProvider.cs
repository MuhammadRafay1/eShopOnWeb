using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

public interface IPayPalTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
    void InvalidateCache();
}
