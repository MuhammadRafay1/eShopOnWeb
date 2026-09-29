using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a stale authorization cannot be renewed (reauthorized) before fulfilment - for
/// example once PayPal's 30-day window since the original authorization has elapsed. Carries
/// PayPal's own issue/description text verbatim so an operator can act on it. Maps to HTTP 409.
/// </summary>
public class PaymentRenewalFailedException : Exception
{
    public PaymentRenewalFailedException(string message) : base(message)
    {
    }
}
