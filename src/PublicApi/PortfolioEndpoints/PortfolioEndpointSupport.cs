using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Shared helpers for the portfolio endpoints: resolving the signed-in shopper from the JWT, and converting
/// domain/provider failures into caller-facing HTTP results (never leaking provider internals or PII).
/// </summary>
public static class PortfolioEndpointSupport
{
    /// <summary>The signed-in shopper's identity (JWT name claim). Endpoints are JWT-authenticated so this is
    /// always present; a missing name is treated as unauthorized.</summary>
    public static string? ResolveShopper(ClaimsPrincipal user) =>
        user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("sub");

    /// <summary>Map an exception thrown by the service to an <see cref="IResult"/>. Returns null when the
    /// exception is not one this layer handles (let it bubble to the global exception middleware).</summary>
    public static IResult? TryMapError(Exception ex) => ex switch
    {
        PortfolioValidationException v => Results.BadRequest(new { error = v.Message }),
        InvestorNotFoundException n => Results.NotFound(new { error = n.Message }),
        ModelNotFoundException n => Results.NotFound(new { error = n.Message }),
        RealignmentNotFoundException n => Results.NotFound(new { error = n.Message }),
        InvestorNotReadyException c => Results.Conflict(new { error = c.Message }),
        DuplicateException d => Results.Conflict(new { error = d.Message }),
        UpvestApiException u => MapUpvest(u),
        _ => null
    };

    private static IResult MapUpvest(UpvestApiException ex)
    {
        // Our own credential/quota problems and transport failures are not the caller's fault -> 5xx.
        // A provider 4xx the caller can act on is surfaced as that same status.
        var status = ex.ProviderStatusCode switch
        {
            401 or 403 => StatusCodes.Status502BadGateway,
            429 => StatusCodes.Status503ServiceUnavailable,
            >= 400 and < 500 => ex.ProviderStatusCode.Value,
            _ => StatusCodes.Status502BadGateway
        };

        return Results.Json(new { error = ex.Message }, statusCode: status);
    }
}
