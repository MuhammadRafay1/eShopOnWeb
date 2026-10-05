using System.Security.Claims;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Resolves the signed-in shopper's identity from the JWT, so each shopper only sees their own data.</summary>
internal static class CallerId
{
    public static string? Resolve(ClaimsPrincipal? user)
    {
        if (user is null)
            return null;

        return user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue("unique_name")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
    }
}
