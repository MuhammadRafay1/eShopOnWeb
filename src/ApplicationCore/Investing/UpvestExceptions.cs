using System;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>Raised when a call to Upvest fails. Never carries personal data in its message.</summary>
public class UpvestException : Exception
{
    public UpvestException(string message, int statusCode = 0) : base(message) => StatusCode = statusCode;
    public UpvestException(string message, Exception inner, int statusCode = 0) : base(message, inner) => StatusCode = statusCode;

    /// <summary>The HTTP status code Upvest returned, or 0 if no response was received.</summary>
    public int StatusCode { get; }
}

/// <summary>Raised when Upvest refuses to take the shopper on as an investor.</summary>
public class UpvestRejectedException : UpvestException
{
    public UpvestRejectedException(string message, int statusCode = 0) : base(message, statusCode) { }
}
