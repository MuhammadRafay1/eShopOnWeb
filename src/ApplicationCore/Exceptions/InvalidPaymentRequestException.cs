using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown for a caller mistake in a payment request — e.g. supplying both or neither of card/saved-card,
/// or a refund amount exceeding what remains refundable. Maps to HTTP 400.
/// </summary>
public class InvalidPaymentRequestException : Exception
{
    public InvalidPaymentRequestException(string message) : base(message)
    {
    }
}
