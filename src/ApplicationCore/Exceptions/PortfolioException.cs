using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>Domain-level error in the managed-portfolio flows, carrying the HTTP status to surface.</summary>
public class PortfolioException : Exception
{
    public int StatusCode { get; }

    public PortfolioException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }

    public static PortfolioException NotFound(string message) => new(message, 404);
    public static PortfolioException Conflict(string message) => new(message, 409);
    public static PortfolioException BadRequest(string message) => new(message, 400);
}
