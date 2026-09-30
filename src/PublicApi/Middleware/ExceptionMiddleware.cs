using System;
using System.Net;
using System.Text.Json;
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

        switch (exception)
        {
            case DuplicateException duplicate:
                await WriteError(context, HttpStatusCode.Conflict, duplicate.Message);
                break;

            case ResourceNotFoundException notFound:
                await WriteError(context, HttpStatusCode.NotFound, notFound.Message);
                break;

            // A payment action failed for a reason the client can branch on (e.g. a stale
            // authorization that can no longer be renewed) — surface the stable code alongside
            // a human-readable message.
            case PaymentActionException paymentAction:
                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    statusCode = context.Response.StatusCode,
                    error = paymentAction.Code,
                    message = paymentAction.Message
                }));
                break;

            case RefundExceedsCaptureException refundExceeds:
                await WriteError(context, HttpStatusCode.Conflict, refundExceeds.Message);
                break;

            case OrderStateException orderState:
                await WriteError(context, HttpStatusCode.Conflict, orderState.Message);
                break;

            // An upstream failure at PayPal — pass through the parsed error so an operator can act.
            case PayPalApiException payPal:
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    statusCode = context.Response.StatusCode,
                    error = payPal.Name,
                    message = payPal.Message,
                    issues = payPal.Issues,
                    debugId = payPal.DebugId
                }));
                break;

            case ArgumentException argument:
                await WriteError(context, HttpStatusCode.BadRequest, argument.Message);
                break;

            default:
                await WriteError(context, HttpStatusCode.InternalServerError, exception.Message);
                break;
        }
    }

    private static async Task WriteError(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(new ErrorDetails
        {
            StatusCode = context.Response.StatusCode,
            Message = message
        }.ToString());
    }
}
