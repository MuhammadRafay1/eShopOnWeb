using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Helpers for resolving the signed-in shopper from the JWT principal.</summary>
public static class PortfolioCaller
{
    /// <summary>
    /// The stable identifier of the signed-in shopper (their user name / token subject).
    /// Used only to scope each shopper to their own investor record – never written to logs.
    /// </summary>
    public static string? UserName(this ClaimsPrincipal? principal) =>
        principal?.Identity?.Name
        ?? principal?.FindFirstValue(ClaimTypes.Name)
        ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public static IResult? RequireUserName(this ClaimsPrincipal? principal, out string userName)
    {
        var name = principal.UserName();
        userName = name ?? string.Empty;
        return string.IsNullOrEmpty(name) ? Results.Unauthorized() : null;
    }
}
