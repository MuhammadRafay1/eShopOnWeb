namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// Lifecycle of an order with respect to payment and fulfilment.
/// </summary>
public enum OrderStatus
{
    /// <summary>Order placed, no money held yet.</summary>
    AwaitingPayment = 0,

    /// <summary>Funds have been authorized (held) with PayPal but not captured.</summary>
    PaymentAuthorized = 1,

    /// <summary>Order fulfilled and the authorized funds captured.</summary>
    Fulfilled = 2,

    /// <summary>Cancelled before fulfilment; any authorization was voided so no money moved.</summary>
    Cancelled = 3,

    /// <summary>Fulfilled, then part of the captured amount was refunded.</summary>
    PartiallyRefunded = 4,

    /// <summary>Fulfilled, then the full captured amount was refunded.</summary>
    Refunded = 5
}
