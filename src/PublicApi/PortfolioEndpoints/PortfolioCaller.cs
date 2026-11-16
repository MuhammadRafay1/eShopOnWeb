using System.Security.Claims;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

internal static class PortfolioCaller
{
    /// <summary>
    /// The identity of the signed-in shopper (from the JWT). Every shopper-scoped record is keyed
    /// on this, so one shopper can never see another's.
    /// </summary>
    public static string ShopperId(ClaimsPrincipal user)
    {
        var id = user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(id))
        {
            throw new PortfolioException("The caller could not be identified.", 401);
        }
        return id;
    }
}
