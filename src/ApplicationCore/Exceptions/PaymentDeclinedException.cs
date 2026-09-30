using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal declines a payment operation (authorize/capture/refund/vault) with a client-side
/// (4xx) error the caller could act on. Carries only the PayPal-supplied error name/description — never
/// any card data. Maps to HTTP 402.
/// </summary>
public class PaymentDeclinedException : Exception
{
    /// <summary>PayPal's machine-readable error name (e.g. "INSTRUMENT_DECLINED"), when available.</summary>
    public string? PayPalErrorName { get; }

    public PaymentDeclinedException(string message, string? payPalErrorName = null, Exception? innerException = null)
        : base(message, innerException)
    {
        PayPalErrorName = payPalErrorName;
    }
}
