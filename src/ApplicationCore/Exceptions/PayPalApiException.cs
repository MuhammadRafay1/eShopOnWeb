using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Wraps a failure returned by the PayPal API so callers (and, ultimately, operators) can see
/// PayPal's own error name/issue/message and debug_id rather than a generic failure.
/// </summary>
public class PayPalApiException : Exception
{
    public string? PayPalName { get; }
    public string? DebugId { get; }
    public int? PayPalStatusCode { get; }
    public bool IsRetryable { get; }

    public PayPalApiException(string message, string? payPalName = null, string? debugId = null, int? payPalStatusCode = null, bool isRetryable = false)
        : base(message)
    {
        PayPalName = payPalName;
        DebugId = debugId;
        PayPalStatusCode = payPalStatusCode;
        IsRetryable = isRetryable;
    }
}
