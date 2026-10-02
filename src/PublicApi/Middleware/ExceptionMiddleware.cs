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

        if (exception is DuplicateException duplicationException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            await context.Response.WriteAsync(new ErrorDetails()
            {
                StatusCode = context.Response.StatusCode,
                Message = duplicationException.Message
            }.ToString());
        }
        else if (exception is PaymentException paymentException)
        {
            // Payment-flow errors carry their own caller-facing status and a safe message.
            context.Response.StatusCode = paymentException.StatusCode;
            await context.Response.WriteAsync(new ErrorDetails()
            {
                StatusCode = context.Response.StatusCode,
                Message = paymentException.Message
            }.ToString());
        }
        else if (exception is PaymentGatewayException gatewayException)
        {
            // A provider failure the caller cannot fix (transport, our credentials, PayPal 5xx) — a 4xx the
            // caller caused is already remapped to a PaymentException before it reaches here.
            context.Response.StatusCode = gatewayException.IsCallerError && gatewayException.ProviderStatusCode is int s
                ? s
                : (int)HttpStatusCode.BadGateway;
            await context.Response.WriteAsync(new ErrorDetails()
            {
                StatusCode = context.Response.StatusCode,
                Message = "The payment provider is currently unavailable. Please try again."
            }.ToString());
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsync(new ErrorDetails()
            {
                StatusCode = context.Response.StatusCode,
                Message = exception.Message
            }.ToString());
        }
    }
}
