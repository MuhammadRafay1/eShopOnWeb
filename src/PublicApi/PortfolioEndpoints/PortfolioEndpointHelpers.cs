using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Shared helpers for the portfolio endpoints: caller identity and error mapping.</summary>
internal static class PortfolioEndpointHelpers
{
    /// <summary>
    /// Requires an authenticated caller on the JWT bearer scheme. Naming the scheme is essential: the
    /// host also registers Identity cookies, whose challenge would otherwise 302-redirect API callers.
    /// </summary>
    public static RouteHandlerBuilder RequireJwt(this RouteHandlerBuilder builder) =>
        builder.RequireAuthorization(policy => policy
            .RequireAuthenticatedUser()
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme));

    /// <summary>
    /// The caller's identity (JWT name), used as the buyerId that scopes all of their portfolio data.
    /// </summary>
    public static string? GetBuyerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name;

    /// <summary>
    /// Runs a handler, translating portfolio domain exceptions into the matching HTTP result so each
    /// endpoint stays focused on its flow. Provider failures are already handled inside the service.
    /// </summary>
    public static async Task<IResult> RunAsync(ClaimsPrincipal user, Func<string, Task<IResult>> handler)
    {
        var buyerId = GetBuyerId(user);
        if (string.IsNullOrEmpty(buyerId))
            return Results.Unauthorized();

        try
        {
            return await handler(buyerId);
        }
        catch (PortfolioValidationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (PortfolioNotFoundException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (PortfolioConflictException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    /// <summary>Overload for operator actions that do not act for a specific caller.</summary>
    public static async Task<IResult> RunAsync(Func<Task<IResult>> handler)
    {
        try
        {
            return await handler();
        }
        catch (PortfolioValidationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (PortfolioNotFoundException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (PortfolioConflictException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
