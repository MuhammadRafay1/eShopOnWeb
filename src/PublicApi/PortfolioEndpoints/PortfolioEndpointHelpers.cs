using System;
using System.Linq;
using System.Security.Claims;
using Ardalis.Result;
using Microsoft.AspNetCore.Http;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Shared helpers: caller identity extraction and Ardalis.Result → HTTP mapping.</summary>
internal static class PortfolioEndpointHelpers
{
    /// <summary>The signed-in shopper's identity (the JWT name claim), used to scope every record.</summary>
    public static string? GetBuyerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name;

    public static IResult ToHttpResult<T>(Result<T> result, Func<T, object> project, int successStatusCode = StatusCodes.Status200OK)
    {
        return result.Status switch
        {
            ResultStatus.Ok => successStatusCode == StatusCodes.Status201Created
                ? Results.Json(project(result.Value), statusCode: StatusCodes.Status201Created)
                : Results.Ok(project(result.Value)),
            ResultStatus.NotFound => Results.NotFound(new { message = FirstMessage(result, "Not found.") }),
            ResultStatus.Invalid => Results.BadRequest(new { errors = result.ValidationErrors.Select(e => e.ErrorMessage) }),
            ResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            ResultStatus.Unauthorized => Results.StatusCode(StatusCodes.Status401Unauthorized),            _ => Results.Problem(detail: FirstMessage(result, "An unexpected error occurred."), statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    public static IResult ToHttpResult(Result result)
    {
        return result.Status switch
        {
            ResultStatus.Ok => Results.NoContent(),
            ResultStatus.NotFound => Results.NotFound(new { message = FirstMessage(result, "Not found.") }),
            ResultStatus.Invalid => Results.BadRequest(new { errors = result.ValidationErrors.Select(e => e.ErrorMessage) }),
            ResultStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            ResultStatus.Unauthorized => Results.StatusCode(StatusCodes.Status401Unauthorized),            _ => Results.Problem(detail: FirstMessage(result, "An unexpected error occurred."), statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    public static IResult Unauthenticated() =>
        Results.StatusCode(StatusCodes.Status401Unauthorized);

    public static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string FirstMessage<T>(Result<T> result, string fallback) =>
        result.Errors.FirstOrDefault() ?? fallback;

    private static string FirstMessage(Result result, string fallback) =>
        result.Errors.FirstOrDefault() ?? fallback;
}
