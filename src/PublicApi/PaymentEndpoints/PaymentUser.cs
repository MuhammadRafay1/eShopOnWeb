using System.Security.Claims;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

internal static class PaymentUser
{
    /// <summary>The caller's buyer id (their username/email), taken from the JWT. Never a client-supplied value.</summary>
    public static string BuyerId(ClaimsPrincipal user)
    {
        var buyerId = user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
        {
            throw new PaymentException("The caller identity could not be determined from the token.", 401);
        }
        return buyerId;
    }
}
