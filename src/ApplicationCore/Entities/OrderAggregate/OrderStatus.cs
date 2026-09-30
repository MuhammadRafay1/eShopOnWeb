namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// The fulfilment / payment lifecycle state of an <see cref="Order"/>.
/// This is the one status enum owned by eShop (PayPal's own resource statuses are
/// persisted verbatim on the Payment aggregate). Existing Web-created orders default
/// to <see cref="AwaitingPayment"/> and never leave it, keeping the change additive.
/// </summary>
public enum OrderStatus
{
    AwaitingPayment = 0,
    PaymentAuthorized = 1,
    Fulfilled = 2,
    Cancelled = 3,
    PartiallyRefunded = 4,
    Refunded = 5
}
