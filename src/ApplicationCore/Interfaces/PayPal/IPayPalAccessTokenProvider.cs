using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>
/// Provides a cached OAuth2 client-credentials access token for PayPal (POST /v1/oauth2/token).
/// </summary>
public interface IPayPalAccessTokenProvider
{
    /// <param name="forceRefresh">Bypass the cache (e.g. after a downstream 401) and fetch a fresh token.</param>
    Task<string> GetAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}
