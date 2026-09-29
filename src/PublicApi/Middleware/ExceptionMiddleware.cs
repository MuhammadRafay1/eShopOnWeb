using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using BlazorShared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
            case ResourceNotFoundException notFound:
                await WriteAsync(context, HttpStatusCode.NotFound, notFound.Message);
                break;

            case PaymentValidationException validation:
                await WriteAsync(context, HttpStatusCode.UnprocessableEntity, validation.Message);
                break;

            case OrderItemsRequiredException itemsRequired:
                await WriteAsync(context, HttpStatusCode.UnprocessableEntity, itemsRequired.Message);
                break;

            case InvalidOrderItemException invalidItem:
                await WriteAsync(context, HttpStatusCode.UnprocessableEntity, invalidItem.Message);
                break;

            case AuthorizationUnrenewableException unrenewable:
                // A documented, operator-actionable condition — 409 with a structured body.
                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    statusCode = (int)HttpStatusCode.Conflict,
                    code = AuthorizationUnrenewableException.ErrorCode,
                    message = unrenewable.Message,
                    guidance = unrenewable.Guidance,
                }));
                break;

            case PaymentConflictException conflict:
                await WriteAsync(context, HttpStatusCode.Conflict, conflict.Message);
                break;

            case DuplicateException duplicationException:
                await WriteAsync(context, HttpStatusCode.Conflict, duplicationException.Message);
                break;

            case PayPalPayerActionRequiredException payerAction:
                // Task-mandated STOP condition — surface loudly, do not degrade.
                _logger.LogError(payerAction, "PayPal payer-action/challenge encountered.");
                await WriteAsync(context, HttpStatusCode.InternalServerError, payerAction.Message);
                break;

            case PayPalApiException payPalError:
                _logger.LogError("PayPal upstream error: status={Status} name={Name} debug_id={DebugId} details={Details}",
                    payPalError.HttpStatus, payPalError.Name, payPalError.DebugId, payPalError.Details);
                // Do not surface PayPal's raw message to shopper-facing callers.
                await WriteAsync(context, HttpStatusCode.BadGateway,
                    "The payment provider could not process this request. Please try again later.");
                break;

            default:
                _logger.LogError(exception, "Unhandled exception.");
                await WriteAsync(context, HttpStatusCode.InternalServerError, exception.Message);
                break;
        }
    }

    private static Task WriteAsync(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.StatusCode = (int)statusCode;
        return context.Response.WriteAsync(new ErrorDetails
        {
            StatusCode = context.Response.StatusCode,
            Message = message,
        }.ToString());
    }
}
