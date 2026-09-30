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

        var (statusCode, message) = Classify(exception);
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsync(new ErrorDetails
        {
            StatusCode = statusCode,
            Message = message
        }.ToString());
    }

    private static (int StatusCode, string Message) Classify(Exception exception) => exception switch
    {
        DuplicateException => ((int)HttpStatusCode.Conflict, exception.Message),

        OrderNotFoundException or PaymentMethodNotFoundException or CatalogItemNotFoundException
            => ((int)HttpStatusCode.NotFound, exception.Message),

        InvalidPaymentTransitionException or AuthorizationNotRenewableException
            => ((int)HttpStatusCode.Conflict, exception.Message),

        RefundExceedsCapturedException or PaymentChallengeRequiredException
            => ((int)HttpStatusCode.UnprocessableEntity, exception.Message),

        PaymentDeclinedException => ((int)HttpStatusCode.PaymentRequired, exception.Message),

        ArgumentException => ((int)HttpStatusCode.BadRequest, exception.Message),

        PayPalApiException payPalEx => ClassifyPayPalException(payPalEx),

        _ => ((int)HttpStatusCode.InternalServerError, exception.Message)
    };

    private static (int StatusCode, string Message) ClassifyPayPalException(PayPalApiException ex)
    {
        // debug_id/name are safe to surface (never card data) and are what an operator needs to
        // look the failure up with PayPal support.
        var message = $"{ex.Message}" +
            (ex.Name is null ? string.Empty : $" (PayPal error: {ex.Name})") +
            (ex.DebugId is null ? string.Empty : $" [debug_id={ex.DebugId}]");

        if (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // PayPal rejected our credentials/permissions - an upstream configuration problem,
            // not something the caller of this API can fix.
            return ((int)HttpStatusCode.BadGateway, message);
        }

        if ((int)ex.StatusCode is >= 400 and < 500)
        {
            return ((int)HttpStatusCode.UnprocessableEntity, message);
        }

        return ((int)HttpStatusCode.BadGateway, message);
    }
}
