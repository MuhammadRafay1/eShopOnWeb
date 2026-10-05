using System;
using System.Net;

namespace Microsoft.eShopWeb.PublicApi.Investing.Upvest;

/// <summary>Raised when a call to Upvest fails. Never carries personal data.</summary>
public class UpvestException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public UpvestException(string message, HttpStatusCode? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
