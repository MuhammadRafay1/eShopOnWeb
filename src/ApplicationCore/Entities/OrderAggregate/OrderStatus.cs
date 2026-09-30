namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// Lifecycle state of an <see cref="Order"/> for the payment/fulfilment flow.
/// The legal transitions between these values are enforced by the Order entity itself.
/// </summary>
public enum OrderStatus
{
    /// <summary>Order placed, no payment authorized yet.</summary>
    AwaitingPayment = 0,

    /// <summary>Funds authorized (held) with PayPal, not yet captured.</summary>
    Authorized = 1,

    /// <summary>Cancelled before fulfilment; any hold was released.</summary>
    Cancelled = 2,

    /// <summary>Fulfilled by an operator; funds captured.</summary>
    Fulfilled = 3,

    /// <summary>Captured funds partially refunded.</summary>
    PartiallyRefunded = 4,

    /// <summary>Captured funds fully refunded.</summary>
    Refunded = 5
}
