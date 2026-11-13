using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;
using Microsoft.eShopWeb.PublicApi.Portfolio.Upvest;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.PortfolioEndpoints;

/// <summary>Shared helpers for the portfolio endpoints: caller identity and exception-to-HTTP mapping.</summary>
public static class EndpointSupport
{
    public const string Tag = "PortfolioEndpoints";

    /// <summary>The signed-in shopper's identity from the JWT name claim. Never from the request body.</summary>
    public static string? GetShopperId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name;

    /// <summary>
    /// Runs an endpoint body and maps our known failures onto HTTP results. Caller-fixable errors keep their status;
    /// provider failures become 502/503 (the caller cannot fix our credentials or quota). Messages are caller-safe.
    /// </summary>
    public static async Task<IResult> RunAsync(ClaimsPrincipal user, Func<string, Task<IResult>> body)
    {
        var shopperId = GetShopperId(user);
        if (string.IsNullOrEmpty(shopperId))
            return Results.Unauthorized();

        try
        {
            return await body(shopperId);
        }
        catch (PortfolioRequestException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: ex.StatusCode);
        }
        catch (UpvestProviderException ex)
        {
            var status = MapProviderStatus(ex);
            return Results.Problem(detail: "The investment provider could not complete the request.", statusCode: status);
        }
    }

    private static int MapProviderStatus(UpvestProviderException ex)
    {
        // Our credentials/scope or quota — the caller did nothing wrong and cannot fix it.
        if (ex.UnauthorizedScope)
            return StatusCodes.Status502BadGateway;
        var code = (int?)ex.StatusCode;
        if (code == 429)
            return StatusCodes.Status503ServiceUnavailable;
        if (code is >= 400 and < 500)
            return code.Value; // a genuine caller-side rejection the provider reported
        return StatusCodes.Status502BadGateway; // transport, timeout, 5xx, unknown
    }
}
