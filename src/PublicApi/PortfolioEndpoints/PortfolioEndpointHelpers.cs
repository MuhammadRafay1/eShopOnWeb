using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Shared helpers for the portfolio endpoints.</summary>
internal static class PortfolioEndpointHelpers
{
    /// <summary>
    /// The stable identity of the signed-in shopper, taken from the JWT. Every per-shopper
    /// operation is scoped by this value so one shopper can never see another's data.
    /// </summary>
    public static string ShopperId(HttpContext context)
    {
        string? id = context.User.Identity?.Name
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(id))
            throw new PortfolioException(401, "The caller identity could not be determined from the token.");
        return id;
    }
}

/// <summary>Carries the signed-in shopper id and the request cancellation token into a handler.</summary>
public abstract class ShopperRequest
{
    public string ShopperId { get; set; } = string.Empty;
    public CancellationToken CancellationToken { get; set; }
}
