using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

internal static class PortfolioEndpointHelpers
{
    /// <summary>
    /// The caller's identity, taken from the JWT. Scopes every shopper-owned resource so one
    /// shopper can never see another's.
    /// </summary>
    public static string BuyerId(this ClaimsPrincipal user)
    {
        var name = user.Identity?.Name
                   ?? user.FindFirstValue(ClaimTypes.Name)
                   ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw PortfolioException.BadRequest("The access token does not identify a shopper.");
        }
        return name;
    }

    /// <summary>
    /// Money amounts are euros with two decimal places. Rounding to 2 and adding a scale-2 zero
    /// fixes the decimal's scale at 2 so System.Text.Json renders it as a two-decimal number.
    /// </summary>
    public static decimal Money(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero) + 0.00m;

    public static IResult NotFoundResult(string message) => Results.NotFound(new { message });
}
