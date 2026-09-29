using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when an order cannot be fulfilled because its PayPal authorization has gone stale and can
/// no longer be renewed (the 29-day reauthorization ceiling has passed, or the hold was already
/// voided/captured). Carries PayPal's own error info so an operator can act on it (e.g. cancel the
/// order and have the shopper pay again). Maps to HTTP 409.
/// </summary>
public class OrderFulfilmentException : Exception
{
    public string? PayPalName { get; }
    public IReadOnlyList<PayPalErrorDetail> Details { get; }

    public OrderFulfilmentException(string message, string? payPalName, IReadOnlyList<PayPalErrorDetail>? details)
        : base(message)
    {
        PayPalName = payPalName;
        Details = details ?? Array.Empty<PayPalErrorDetail>();
    }
}
