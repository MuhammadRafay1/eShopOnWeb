using System.Security.Claims;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Resolves the signed-in shopper's identity from the JWT. This identity scopes every investor,
/// payment and portfolio record so one shopper can never see another's.
/// </summary>
public static class PortfolioCaller
{
    public static string? GetShopperId(ClaimsPrincipal? user)
    {
        if (user is null) return null;
        return user.FindFirstValue(ClaimTypes.Name)
               ?? user.Identity?.Name
               ?? user.FindFirstValue("unique_name")
               ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
