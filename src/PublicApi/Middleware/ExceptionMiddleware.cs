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

        var (statusCode, message) = Map(exception);
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsync(new ErrorDetails()
        {
            StatusCode = statusCode,
            Message = message
        }.ToString());
    }

    private static (int statusCode, string message) Map(Exception exception) => exception switch
    {
        BadRequestException => ((int)HttpStatusCode.BadRequest, exception.Message),
        NotFoundException => ((int)HttpStatusCode.NotFound, exception.Message),
        ConflictException => ((int)HttpStatusCode.Conflict, exception.Message),
        DuplicateException => ((int)HttpStatusCode.Conflict, exception.Message),
        UnprocessableEntityException => (422, exception.Message),
        PayPalChallengeRequiredException => (422, exception.Message),
        PayPalApiException papEx => (MapPayPal(papEx), Describe(papEx)),
        _ => ((int)HttpStatusCode.InternalServerError, exception.Message)
    };

    private static int MapPayPal(PayPalApiException ex) => ex.HttpStatusCode switch
    {
        429 => (int)HttpStatusCode.ServiceUnavailable,   // 503 — retryable
        >= 500 => (int)HttpStatusCode.BadGateway,         // 502 — upstream failure
        _ => 422                                          // 4xx business/validation (e.g. card declined)
    };

    private static string Describe(PayPalApiException ex)
    {
        var issues = ex.DescribeIssues();
        return ex.DebugId is null ? issues : $"{issues} (PayPal debug_id: {ex.DebugId})";
    }
}
