using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A client-facing error in the portfolio feature. <see cref="StatusCode"/> is the HTTP status
/// to return and <see cref="Message"/> is a safe, personal-data-free description.
/// </summary>
public class PortfolioException : Exception
{
    public int StatusCode { get; }

    public PortfolioException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public static PortfolioException BadRequest(string message) => new(400, message);
    public static PortfolioException NotFound(string message) => new(404, message);
    public static PortfolioException Conflict(string message) => new(409, message);
}
