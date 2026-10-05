using System.Security.Claims;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

internal static class CallerIdentity
{
    /// <summary>The shopper's identity from the JWT (used as the buyer id across baskets, orders and investing).</summary>
    public static string? GetBuyerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
}
