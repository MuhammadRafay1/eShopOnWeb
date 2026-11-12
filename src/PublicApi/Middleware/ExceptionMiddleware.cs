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
        else if (exception is UpvestIntegrationException upvestException)
        {
            context.Response.StatusCode = MapUpvestStatus(upvestException);
            await context.Response.WriteAsync(new ErrorDetails()
            {
                StatusCode = context.Response.StatusCode,
                Message = upvestException.Message // caller-safe; carries no personal data or secrets
            }.ToString());
        }
        else if (exception is ArgumentException)
        {
            // Invalid caller input that reached a domain guard (e.g. model weights not summing to 100).
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync(new ErrorDetails()
            {
                StatusCode = context.Response.StatusCode,
                Message = exception.Message
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

    private static int MapUpvestStatus(UpvestIntegrationException exception) => exception.Kind switch
    {
        // The provider rejected the caller's request — hand back the same 4xx so they can act on it.
        UpvestFailureKind.CallerError => exception.StatusCode is >= 400 and < 500
            ? exception.StatusCode.Value
            : (int)HttpStatusCode.BadRequest,
        // Our credentials/scopes/quota, or a write whose outcome is unknown: the caller did nothing wrong.
        UpvestFailureKind.ProviderRejectedUs => (int)HttpStatusCode.BadGateway,
        UpvestFailureKind.Unknown => (int)HttpStatusCode.BadGateway,
        // Transport failure or provider 5xx.
        _ => (int)HttpStatusCode.ServiceUnavailable
    };
}
