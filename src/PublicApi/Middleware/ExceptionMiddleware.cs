using System;
using System.Net;
using System.Threading.Tasks;
using BlazorShared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;

namespace Microsoft.eShopWeb.PublicApi.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(httpContext, ex);        
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = Map(exception);
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsync(new ErrorDetails()
        {
            StatusCode = statusCode,
            Message = message
        }.ToString());
    }

    private static (int StatusCode, string Message) Map(Exception exception)
    {
        switch (exception)
        {
            case DuplicateException:
                return ((int)HttpStatusCode.Conflict, exception.Message);

            // Caller-facing portfolio errors carry their intended HTTP status.
            case PortfolioException portfolioException:
                return (portfolioException.StatusCode, portfolioException.Message);

            // Invalid input (e.g. model weights that don't add up to 100).
            case ArgumentException:
                return ((int)HttpStatusCode.BadRequest, exception.Message);

            // A failure returned by Upvest. A scope refusal is the known limitation of this
            // environment's client; surface it as 503 so it reads as "unavailable here", not a bug.
            case UpvestApiException upvestException when upvestException.IsScopeAuthorizationFailure:
                return ((int)HttpStatusCode.ServiceUnavailable,
                    "This capability requires an Upvest permission this environment's client is not granted. " +
                    $"Upvest reported: {upvestException.Detail}");

            case UpvestApiException upvestException:
                return ((int)HttpStatusCode.BadGateway, upvestException.Message);

            default:
                return ((int)HttpStatusCode.InternalServerError, exception.Message);
        }
    }
}
