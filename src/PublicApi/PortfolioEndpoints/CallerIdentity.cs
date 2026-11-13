using System.Security.Claims;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Resolves the signed-in shopper's identity from the JWT. This value scopes every
/// portfolio operation to the caller, so one shopper can never see another's data.
/// </summary>
public static class CallerIdentity
{
    public static string? GetShopperId(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Name)
            ?? user.Identity?.Name
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
