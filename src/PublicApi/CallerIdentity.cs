using System.Security.Claims;

namespace Microsoft.eShopWeb.PublicApi;

/// <summary>
/// Resolves the caller's buyer id from the JWT. The token carries only <see cref="ClaimTypes.Name"/>
/// (the username), which is the same identity string Basket/Order/PaymentMethod key on — so the
/// buyer id is the username everywhere. The identity always comes from the token, never the request.
/// </summary>
public static class CallerIdentity
{
    public static string GetBuyerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name ?? "";
}
