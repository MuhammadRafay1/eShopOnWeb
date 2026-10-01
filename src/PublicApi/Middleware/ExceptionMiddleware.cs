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
        context.Response.StatusCode = StatusCodeFor(exception);

        await context.Response.WriteAsync(new ErrorDetails()
        {
            StatusCode = context.Response.StatusCode,
            Message = exception.Message
        }.ToString());
    }

    private static int StatusCodeFor(Exception exception) => exception switch
    {
        DuplicateException => (int)HttpStatusCode.Conflict,
        ResourceNotFoundException => (int)HttpStatusCode.NotFound,
        RefundAmountExceededException => (int)HttpStatusCode.UnprocessableEntity,
        AuthorizationExpiredException => (int)HttpStatusCode.Conflict,
        PaymentChallengeRequiredException => (int)HttpStatusCode.UnprocessableEntity,
        PaymentAuthorizationException => (int)HttpStatusCode.Conflict,
        PayPalGatewayException g => StatusCodeForGateway(g),
        PaymentException => (int)HttpStatusCode.BadRequest,
        _ => (int)HttpStatusCode.InternalServerError
    };

    private static int StatusCodeForGateway(PayPalGatewayException gateway)
    {
        // A PayPal 4xx means the caller's request was rejected (surface the same status so they can act
        // on it); anything else (no status, or a PayPal 5xx/connection failure) is this app's problem - 502.
        if (gateway.StatusCode is >= 400 and < 500)
        {
            return gateway.StatusCode.Value;
        }

        return (int)HttpStatusCode.BadGateway;
    }
}
