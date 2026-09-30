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

        // Map each domain exception to a deliberate status. A PayPal-side problem (502) is kept distinct from
        // a generic 500 so a caller does not retry a deterministic rejection, and never from the raw exception
        // message so no internal or card detail can leak.
        var (statusCode, message) = exception switch
        {
            OrderNotFoundException => (HttpStatusCode.NotFound, exception.Message),
            PaymentMethodNotFoundException => (HttpStatusCode.NotFound, exception.Message),
            InvalidPaymentRequestException => (HttpStatusCode.BadRequest, exception.Message),
            PaymentDeclinedException => (HttpStatusCode.PaymentRequired, exception.Message),
            PayerActionRequiredException => (HttpStatusCode.PaymentRequired, exception.Message),
            OrderStateConflictException => (HttpStatusCode.Conflict, exception.Message),
            AuthorizationCannotBeRenewedException => (HttpStatusCode.Conflict, exception.Message),
            DuplicateException => (HttpStatusCode.Conflict, exception.Message),
            PaymentGatewayException => (HttpStatusCode.BadGateway, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(new ErrorDetails()
        {
            StatusCode = context.Response.StatusCode,
            Message = message
        }.ToString());
    }
}
