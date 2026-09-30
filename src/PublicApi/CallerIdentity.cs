using System.Security.Claims;

namespace Microsoft.eShopWeb.PublicApi;

/// <summary>
/// Resolves the caller's shopper identity from the JWT the same way the rest of the app
/// does — the <see cref="ClaimTypes.Name"/> value, which is what Order.BuyerId stores.
/// </summary>
internal static class CallerIdentity
{
    public static string BuyerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name ?? string.Empty;
}
