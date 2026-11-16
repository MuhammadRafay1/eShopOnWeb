using System;
using System.Net;
using System.Threading.Tasks;
using BlazorShared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

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

        var (statusCode, message) = Map(exception);
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsync(new ErrorDetails()
        {
            StatusCode = statusCode,
            Message = message
        }.ToString());
    }

    // One ladder, in one place: map each failure kind to a caller-facing status. Provider auth/throttle
    // failures are ours, not the caller's, so they become 5xx rather than passing 401/403/429 straight through.
    private static (int statusCode, string message) Map(Exception exception) => exception switch
    {
        DuplicateException => ((int)HttpStatusCode.Conflict, exception.Message),

        PortfolioRequestException pre => (pre.StatusCode, pre.Message),

        // A write whose outcome could not be settled: report "not confirmed", not a flat failure.
        UpvestIntegrationException { OutcomeUnknown: true } => (
            (int)HttpStatusCode.BadGateway,
            "The investment provider did not confirm the outcome; please check its current status before retrying."),

        UpvestIntegrationException u => MapProviderStatus(u),

        _ => ((int)HttpStatusCode.InternalServerError, exception.Message)
    };

    private static (int statusCode, string message) MapProviderStatus(UpvestIntegrationException exception)
    {
        var status = (int?)exception.StatusCode;
        return status switch
        {
            // Our credentials / our quota — the caller did nothing wrong and cannot fix it.
            401 or 403 => ((int)HttpStatusCode.BadGateway, "The investment provider is currently unavailable."),
            429 => ((int)HttpStatusCode.ServiceUnavailable, "The investment provider is temporarily unavailable."),
            // The provider rejected the caller's request — surface the same status so they can act on it.
            >= 400 and < 500 => (status.Value, exception.Message),
            // Transport, timeout, provider 5xx — no meaningful caller status.
            _ => ((int)HttpStatusCode.BadGateway, "The investment provider is currently unavailable.")
        };
    }
}
