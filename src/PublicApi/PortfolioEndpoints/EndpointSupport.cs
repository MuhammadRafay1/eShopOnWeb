using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.PublicApi.Portfolio;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Shared endpoint plumbing: resolving the caller's identity from the JWT, and a single coherent
/// mapping from application/provider failures to HTTP results (so one shopper's data is never shown
/// to another, and provider auth/quota failures never masquerade as the caller's fault).
/// </summary>
public static class EndpointSupport
{
    /// <summary>The signed-in shopper's identity (JWT name); the key for all per-shopper data.</summary>
    public static string ShopperId(this ClaimsPrincipal user)
    {
        var id = user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(id))
            throw new PortfolioValidationException("The caller identity could not be determined.", 401);
        return id;
    }

    public static async Task<IResult> Guarded(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (PortfolioValidationException ex)
        {
            return Results.Problem(title: ex.Message, statusCode: ex.StatusCode);
        }
        catch (UpvestGatewayException ex)
        {
            var (status, message) = MapProvider(ex);
            return Results.Problem(title: message, statusCode: status);
        }
    }

    private static (int Status, string Message) MapProvider(UpvestGatewayException ex)
    {
        var code = ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : (int?)null;
        return code switch
        {
            // Our credentials / our quota — the caller did nothing wrong and cannot fix it.
            401 or 403 => (502, "The investment provider is unavailable."),
            429 => (503, "The investment provider is temporarily unavailable."),
            // The provider rejected the caller's request — surface the same status so they can act.
            >= 400 and < 500 => (code!.Value, ex.Message),
            // Transport, timeout, provider 5xx, unknown — no meaningful caller status.
            _ => (502, ex.Message),
        };
    }
}
