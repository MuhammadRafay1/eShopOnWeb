using System;
using System.Net;
using System.Threading.Tasks;
using BlazorShared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;

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
            case PortfolioValidationException:
                return ((int)HttpStatusCode.BadRequest, exception.Message);
            case PortfolioNotFoundException:
                return ((int)HttpStatusCode.NotFound, exception.Message);
            case PortfolioConflictException:
                return ((int)HttpStatusCode.Conflict, exception.Message);
            case UpvestIntegrationException upvest:
                // The caller's own credentials/quota are our problem, not theirs; a provider 4xx the caller
                // can act on is surfaced as that same status; everything else is an upstream failure.
                var status = (int?)upvest.StatusCode;
                if (status is 401 or 403 or 429)
                {
                    return ((int)HttpStatusCode.BadGateway, "The investment provider is currently unavailable.");
                }
                if (status is >= 400 and < 500)
                {
                    return (status.Value, "The investment provider rejected the request.");
                }
                return ((int)HttpStatusCode.BadGateway, "The investment provider is currently unavailable.");
            default:
                return ((int)HttpStatusCode.InternalServerError, exception.Message);
        }
    }
}
