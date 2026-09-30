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

        var (statusCode, message) = MapException(exception);
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsync(new ErrorDetails()
        {
            StatusCode = statusCode,
            Message = message
        }.ToString());
    }

    private static (int StatusCode, string Message) MapException(Exception exception) => exception switch
    {
        DuplicateException duplicate => ((int)HttpStatusCode.Conflict, duplicate.Message),

        OrderNotFoundException notFound => ((int)HttpStatusCode.NotFound, notFound.Message),
        SavedPaymentMethodNotFoundException notFound => ((int)HttpStatusCode.NotFound, notFound.Message),

        InvalidOrderRequestException invalidRequest => ((int)HttpStatusCode.BadRequest, invalidRequest.Message),

        InvalidOrderStateException invalidState => ((int)HttpStatusCode.Conflict, invalidState.Message),
        AuthorizationNotRenewableException notRenewable => ((int)HttpStatusCode.Conflict, FormatWithDebugId(notRenewable.Message, notRenewable.DebugId)),

        RefundExceedsCaptureException refundExceeds => (422, refundExceeds.Message),
        PaymentChallengeRequiredException challenge => (422, FormatWithDebugId(challenge.Message, challenge.DebugId)),

        PayPalApiException payPalError => (502, FormatWithDebugId($"PayPal request failed: {payPalError.Message}", payPalError.DebugId)),

        _ => ((int)HttpStatusCode.InternalServerError, exception.Message)
    };

    private static string FormatWithDebugId(string message, string? debugId) =>
        string.IsNullOrEmpty(debugId) ? message : $"{message} (PayPal debug_id: {debugId})";
}
