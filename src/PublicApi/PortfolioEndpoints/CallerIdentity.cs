using System.Security.Claims;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

internal static class CallerIdentity
{
    /// <summary>
    /// The shopper id for the authenticated caller, taken from the JWT name claim. Every
    /// shopper-scoped portfolio resource is keyed by this value so one shopper can never see
    /// another's data.
    /// </summary>
    public static string? GetShopperId(this ClaimsPrincipal user)
        => user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("unique_name");
}
