using System.Security.Claims;

namespace Microsoft.eShopWeb.PublicApi.Investing;

internal static class CurrentUser
{
    /// <summary>The signed-in shopper's buyer id, taken from the JWT (the name claim).</summary>
    public static string? BuyerId(ClaimsPrincipal? user) => user?.Identity?.Name;
}
