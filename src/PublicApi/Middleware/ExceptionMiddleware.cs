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

    private static HttpStatusCode StatusCodeFor(BillingProviderException exception) => exception.Kind switch
    {
        // Maxio did not answer in time (or the outcome of a write could not be confirmed in time).
        BillingFailureKind.Timeout => HttpStatusCode.GatewayTimeout,
        BillingFailureKind.Unreachable => HttpStatusCode.ServiceUnavailable,
        BillingFailureKind.RateLimited => HttpStatusCode.ServiceUnavailable,
        // Maxio rejected what the caller asked for — they can act on it.
        BillingFailureKind.Rejected => HttpStatusCode.UnprocessableEntity,
        // Our credentials/configuration, or a provider failure: nothing the caller can fix.
        _ => HttpStatusCode.BadGateway,
    };

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        if (exception is DuplicateException duplicationException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            await context.Response.WriteAsync(new ErrorDetails()
            {
                StatusCode = context.Response.StatusCode,
                Message = duplicationException.Message
            }.ToString());
        }
        else if (exception is BillingOperationInProgressException inProgressException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            await context.Response.WriteAsync(new ErrorDetails()
            {
                StatusCode = context.Response.StatusCode,
                Message = inProgressException.Message
            }.ToString());
        }
        else if (exception is BillingProviderException billingException)
        {
            // Billing messages are caller-safe by construction (no URLs, credentials or SDK details).
            context.Response.StatusCode = (int)StatusCodeFor(billingException);
            await context.Response.WriteAsync(new ErrorDetails()
            {
                StatusCode = context.Response.StatusCode,
                Message = billingException.Message
            }.ToString());
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsync(new ErrorDetails()
            {
                StatusCode = context.Response.StatusCode,
                Message = exception.Message
            }.ToString());
        }
    }
}
