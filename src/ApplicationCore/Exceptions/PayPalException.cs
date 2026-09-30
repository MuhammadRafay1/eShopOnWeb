using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A failure talking to PayPal, or a PayPal-reported state that this integration cannot resolve on its
/// own. <see cref="StatusCode"/> is the HTTP status the API should return to the caller - it is set by
/// whichever layer first understands the failure well enough to give an operator-actionable message.
/// </summary>
public class PayPalException : Exception
{
    public int StatusCode { get; }
    public string? IssueCode { get; }

    public PayPalException(string message, int statusCode = 502, string? issueCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        IssueCode = issueCode;
    }
}
