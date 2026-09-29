using System;
using System.Collections.Generic;
using System.Linq;
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
                await WriteAsync(context, HttpStatusCode.Conflict, duplicate.Message);
                break;

            case OrderNotFoundException:
            case PaymentMethodNotFoundException:
            case CatalogItemNotFoundException:
                await WriteAsync(context, HttpStatusCode.NotFound, exception.Message);
                break;

            case InvalidPaymentRequestException:
                await WriteAsync(context, HttpStatusCode.BadRequest, exception.Message);
                break;

            case PayPalPayerActionRequiredException:
                // A 3-D Secure / browser approval challenge — surfaced, not worked around.
                await WriteAsync(context, HttpStatusCode.Conflict, exception.Message);
                break;

            case OrderFulfilmentException fulfilment:
                // The hold can no longer be renewed — operator-actionable, with PayPal's own detail.
                await WritePayPalAsync(context, HttpStatusCode.Conflict, fulfilment.Message,
                    fulfilment.PayPalName, fulfilment.Details);
                break;

            case PayPalApiException payPal:
                await WritePayPalAsync(context, MapPayPalStatus(payPal), payPal.Message,
                    payPal.PayPalName, payPal.Details);
                break;

            case InvalidOperationException:
                // Domain state-machine guard (e.g. cancel-after-fulfil, double-fulfil).
                await WriteAsync(context, HttpStatusCode.Conflict, exception.Message);
                break;

            default:
                await WriteAsync(context, HttpStatusCode.InternalServerError, exception.Message);
                break;
        }
    }

    private static HttpStatusCode MapPayPalStatus(PayPalApiException ex)
    {
        // A validation problem PayPal reported about our request is meaningful to the caller;
        // anything else is an upstream failure surfaced as 502.
        return ex.HttpStatusCode switch
        {
            400 or 422 => HttpStatusCode.BadRequest,
            401 or 403 => HttpStatusCode.BadGateway,
            404 => HttpStatusCode.NotFound,
            409 => HttpStatusCode.Conflict,
            _ => HttpStatusCode.BadGateway
        };
    }

    private static Task WriteAsync(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.StatusCode = (int)statusCode;
        return context.Response.WriteAsync(new ErrorDetails
        {
            StatusCode = context.Response.StatusCode,
            Message = message
        }.ToString());
    }

    private static Task WritePayPalAsync(HttpContext context, HttpStatusCode statusCode, string message,
        string? payPalName, IReadOnlyList<PayPalErrorDetail> details)
    {
        context.Response.StatusCode = (int)statusCode;
        var payload = new
        {
            statusCode = context.Response.StatusCode,
            message,
            payPalError = payPalName,
            details = details.Select(d => new { d.Field, d.Issue, d.Description }).ToArray()
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
