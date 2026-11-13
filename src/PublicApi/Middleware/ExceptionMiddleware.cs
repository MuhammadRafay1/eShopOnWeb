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
            case InvestorStateException:
                return ((int)HttpStatusCode.Conflict, exception.Message);
            case PortfolioNotFoundException:
                return ((int)HttpStatusCode.NotFound, exception.Message);
            case UpvestException upvest:
                return MapUpvest(upvest);
            default:
                // Never leak an unexpected exception's details to the caller.
                return ((int)HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
    }

    private static (int StatusCode, string Message) MapUpvest(UpvestException ex)
    {
        // A provider 4xx the caller can act on is surfaced as that same status; our own
        // credential/scope/quota failures and transport/unknown outcomes are 5xx.
        if (ex.CallerFault && ex.ProviderStatusCode is int s and >= 400 and < 500)
        {
            return (s, ex.Message);
        }
        if (ex.ProviderStatusCode == 429)
        {
            return ((int)HttpStatusCode.ServiceUnavailable, ex.Message);
        }
        return ((int)HttpStatusCode.BadGateway, ex.Message);
    }
}
