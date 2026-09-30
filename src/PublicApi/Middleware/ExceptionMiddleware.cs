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

        var statusCode = exception switch
        {
            // Ownership mismatches are surfaced as "not found" so one shopper cannot probe another's ids.
            OrderNotFoundException => HttpStatusCode.NotFound,
            PaymentMethodNotFoundException => HttpStatusCode.NotFound,

            // Wrong lifecycle stage / already-renewable-exhausted are operator-actionable conflicts.
            InvalidOrderStateException => HttpStatusCode.Conflict,
            AuthorizationNotRenewableException => HttpStatusCode.Conflict,
            DuplicateException => HttpStatusCode.Conflict,

            // Refund cap and bad request input.
            RefundAmountExceedsRemainingException => HttpStatusCode.UnprocessableEntity,
            ArgumentException => HttpStatusCode.BadRequest,

            // A card decline is a normal, shopper-actionable outcome.
            PayPalPaymentDeclinedException => HttpStatusCode.PaymentRequired,

            // A challenge / upstream PayPal failure is something to escalate, not a client error.
            PayPalChallengeRequiredException => HttpStatusCode.BadGateway,
            PayPalApiException => HttpStatusCode.BadGateway,

            _ => HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(new ErrorDetails
        {
            StatusCode = context.Response.StatusCode,
            Message = statusCode == HttpStatusCode.InternalServerError
                ? "An unexpected error occurred."
                : exception.Message
        }.ToString());
    }
}
