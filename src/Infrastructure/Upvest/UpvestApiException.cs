using System;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Raised when a call to Upvest fails in a way the caller cannot recover from
/// (a non-success status that is not part of normal flow handling, or a transport error).
/// The message is safe to surface — it never contains personal data or secrets.
/// </summary>
public class UpvestApiException : Exception
{
    public int StatusCode { get; }

    public UpvestApiException(string message, int statusCode = 0, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
