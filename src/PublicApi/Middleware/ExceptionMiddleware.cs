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

    // Single mapping from failure kind → caller-facing status. Provider auth/quota failures are OUR
    // fault (5xx), not the caller's; a provider 4xx the caller can act on is passed through.
    private static (int StatusCode, string Message) Map(Exception exception) => exception switch
    {
        DuplicateException => ((int)HttpStatusCode.Conflict, exception.Message),
        InvestorNotFoundException => ((int)HttpStatusCode.NotFound, exception.Message),
        ModelPortfolioNotFoundException => ((int)HttpStatusCode.NotFound, exception.Message),
        PortfolioValidationException => ((int)HttpStatusCode.BadRequest, exception.Message),
        NotAnAcceptedInvestorException => ((int)HttpStatusCode.Conflict, exception.Message),
        UnauthorizedAccessException => ((int)HttpStatusCode.Unauthorized, "Not authenticated."),
        UpvestProviderException p => MapProvider(p),
        _ => ((int)HttpStatusCode.InternalServerError, exception.Message)
    };

    private static (int StatusCode, string Message) MapProvider(UpvestProviderException ex)
    {
        var status = (int?)ex.StatusCode;
        return status switch
        {
            401 or 403 => ((int)HttpStatusCode.BadGateway, "The investment provider is unavailable."),
            429 => ((int)HttpStatusCode.ServiceUnavailable, "The investment provider is temporarily unavailable."),
            >= 400 and < 500 => (status!.Value, ex.Message),
            _ => ((int)HttpStatusCode.BadGateway, ex.Message)
        };
    }
}
