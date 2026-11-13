using System;
using System.Net;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A portfolio-flow error that carries the HTTP status the caller should receive. Thrown by the
/// portfolio service for expected, caller-facing conditions (unknown investor, not yet accepted,
/// no model chosen, invalid request) so the API surfaces a precise status rather than a 500.
/// </summary>
public class PortfolioException : Exception
{
    public PortfolioException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = (int)statusCode;
    }

    public int StatusCode { get; }

    public static PortfolioException NotFound(string message) =>
        new(HttpStatusCode.NotFound, message);

    public static PortfolioException Conflict(string message) =>
        new(HttpStatusCode.Conflict, message);

    public static PortfolioException BadRequest(string message) =>
        new(HttpStatusCode.BadRequest, message);
}
