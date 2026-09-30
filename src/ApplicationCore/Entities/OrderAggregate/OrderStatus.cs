namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// Fulfilment lifecycle of an <see cref="Order"/>.
/// Money-movement detail (captured / refunded amounts) lives on the order's <see cref="Payment"/>,
/// deliberately kept separate from this "did the shopper get their goods" concern.
/// </summary>
public enum OrderStatus
{
    /// <summary>Order placed, no payment hold taken yet.</summary>
    AwaitingPayment = 0,

    /// <summary>Funds are held (authorized) at PayPal but not yet captured.</summary>
    PaymentAuthorized = 1,

    /// <summary>Order fulfilled; the authorization has been captured (money taken).</summary>
    Fulfilled = 2,

    /// <summary>Order cancelled before fulfilment; any hold was released.</summary>
    Cancelled = 3
}
