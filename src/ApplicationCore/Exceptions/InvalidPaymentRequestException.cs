using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown for caller-input problems in the payment flow that are not PayPal's fault:
/// supplying both a raw card and a saved-card id (or neither), a refund amount exceeding the
/// remaining captured amount, an over-long buyer id, etc. Maps to HTTP 400.
/// </summary>
public class InvalidPaymentRequestException : Exception
{
    public InvalidPaymentRequestException(string message) : base(message)
    {
    }
}
