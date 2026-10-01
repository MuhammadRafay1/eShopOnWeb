using System.Security.Claims;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>Resolves the caller's buyer identity from the JWT (the Name claim), as used across the codebase.</summary>
public static class CallerExtensions
{
    public static string GetBuyerId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name)
        ?? user.Identity?.Name
        ?? string.Empty;
}
