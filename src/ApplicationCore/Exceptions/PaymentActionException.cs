using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A payment operation could not proceed for a reason an operator or shopper can act on.
/// Carries a stable machine-readable <see cref="Code"/> so an API client can branch on it,
/// alongside a human-readable message. Mapped to an HTTP 409 by the API layer.
/// </summary>
public class PaymentActionException : Exception
{
    /// <summary>Stable, machine-readable code, e.g. "authorization_unrenewable".</summary>
    public string Code { get; }

    public PaymentActionException(string code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// A stale authorization could not be renewed, so the order cannot be fulfilled and must be
    /// handled (e.g. cancelled and re-placed) by an operator.
    /// </summary>
    public static PaymentActionException AuthorizationUnrenewable(string detail) =>
        new PaymentActionException("authorization_unrenewable",
            "The payment authorization has expired and could not be renewed, so this order " +
            "cannot be fulfilled. Cancel the order and ask the shopper to pay again. " + detail);

    /// <summary>
    /// PayPal requires a buyer to approve the payment in a browser (e.g. 3-D Secure challenge).
    /// This integration does not build an approval round-trip; the operation is reported instead.
    /// </summary>
    public static PaymentActionException PayerActionRequired() =>
        new PaymentActionException("payer_action_required",
            "PayPal requires the shopper to approve this payment in a browser (challenge/3-D " +
            "Secure). This integration does not support a browser approval round-trip.");
}
