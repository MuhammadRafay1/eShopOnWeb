using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A business-rule violation caught before touching PayPal — e.g. exactly-one-of card/saved-card
/// violated, an over-refund amount, or a bad date range. Maps to 422 Unprocessable Entity.
/// </summary>
public class PaymentValidationException : Exception
{
    public PaymentValidationException(string message) : base(message)
    {
    }
}
