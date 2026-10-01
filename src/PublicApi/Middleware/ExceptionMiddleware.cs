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

        var (statusCode, message) = exception switch
        {
            DuplicateException ex => (HttpStatusCode.Conflict, ex.Message),
            OrderNotFoundException or PaymentMethodNotFoundException => (HttpStatusCode.NotFound, exception.Message),
            OrderPaymentStateException ex => (HttpStatusCode.Conflict, ex.Message),
            RefundAmountExceededException ex => (HttpStatusCode.UnprocessableEntity, ex.Message),
            PaymentGatewayException ex => (MapPaymentGatewayStatus(ex.Reason), ex.Message),
            _ => (HttpStatusCode.InternalServerError, exception.Message)
        };

        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(new ErrorDetails()
        {
            StatusCode = context.Response.StatusCode,
            Message = message
        }.ToString());
    }

    private static HttpStatusCode MapPaymentGatewayStatus(PaymentGatewayFailureReason reason) => reason switch
    {
        PaymentGatewayFailureReason.ChallengeRequired => HttpStatusCode.UnprocessableEntity,
        PaymentGatewayFailureReason.ProviderRejected => HttpStatusCode.UnprocessableEntity,
        PaymentGatewayFailureReason.ReauthorizationExpired => HttpStatusCode.Conflict,
        PaymentGatewayFailureReason.Unavailable => HttpStatusCode.BadGateway,
        _ => HttpStatusCode.BadGateway
    };
}
