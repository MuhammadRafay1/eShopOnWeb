using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal (or our own ledger guard) rejects a payment operation for a business reason
/// the caller/operator can act on — an over-refund attempt, a stale authorization that can no longer
/// be renewed, or a payer-action/3DS challenge this integration does not support. Maps to HTTP 422.
/// </summary>
public class PaymentRejectedException : Exception
{
    public PaymentRejectedException(string message) : base(message)
    {
    }
}
