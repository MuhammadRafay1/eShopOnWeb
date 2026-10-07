using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Reads the signed-in shopper's identity from the JWT, never from the request body.</summary>
public static class CallerIdentity
{
    /// <summary>
    /// The shopper's identity (their username/email), taken from the token's name claim. This is the
    /// <c>buyerId</c> every investing and order operation is scoped to.
    /// </summary>
    public static string? GetBuyerId(IHttpContextAccessor accessor)
    {
        var user = accessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name;
    }
}
