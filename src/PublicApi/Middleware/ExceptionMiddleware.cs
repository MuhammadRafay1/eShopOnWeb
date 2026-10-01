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

        var (status, message) = Map(exception);
        context.Response.StatusCode = (int)status;
        await context.Response.WriteAsync(new ErrorDetails()
        {
            StatusCode = context.Response.StatusCode,
            Message = message
        }.ToString());
    }

    private static (HttpStatusCode Status, string Message) Map(Exception exception)
    {
        switch (exception)
        {
            case DuplicateException:
                return (HttpStatusCode.Conflict, exception.Message);

            // Not found / not owned — same answer either way, so one shopper can't probe another's data.
            case OrderNotFoundException:
            case PaymentMethodNotFoundException:
                return (HttpStatusCode.NotFound, exception.Message);

            // Illegal state transition, or a caller mistake we can describe safely.
            case InvalidOrderStateException:
                return (HttpStatusCode.Conflict, exception.Message);
            case OverRefundException:
            case ArgumentException:
                return (HttpStatusCode.BadRequest, exception.Message);

            // PayPal told us a payer-action/3DS challenge is needed — a stop condition, not a retry loop.
            case PayPalPayerActionRequiredException:
                return (HttpStatusCode.Conflict, exception.Message);

            // A write whose outcome could not be confirmed — the operator must reconcile, not blindly retry.
            case PaymentOutcomeUnknownException:
                return (HttpStatusCode.Accepted, exception.Message);

            case PaymentGatewayException gatewayException:
                // A provider 4xx the caller can act on (e.g. a declined card) maps to a client error;
                // credential/throttling and everything else is an upstream failure.
                var code = (int?)gatewayException.StatusCode;
                if (code is 400 or 409 or 422)
                    return (HttpStatusCode.BadRequest, exception.Message);
                return (HttpStatusCode.BadGateway, exception.Message);

            default:
                return (HttpStatusCode.InternalServerError, exception.Message);
        }
    }
}
