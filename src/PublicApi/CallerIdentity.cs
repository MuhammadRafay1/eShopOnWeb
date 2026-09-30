using System.Security.Claims;

namespace Microsoft.eShopWeb.PublicApi;

/// <summary>
/// Resolves the caller's identity from the validated JWT. The identity string (the
/// <see cref="ClaimTypes.Name"/> claim that IdentityTokenClaimService puts in the token) is the
/// same value used as BuyerId across the app — it is never taken from a request body.
/// </summary>
public static class CallerIdentity
{
    public static string GetBuyerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
}
