namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// Fulfilment / payment lifecycle of an <see cref="Order"/>. Additive to the existing
/// order flow: an order created through the storefront basket checkout keeps the default
/// <see cref="AwaitingPayment"/> value and is unaffected by the payment endpoints.
/// </summary>
public enum OrderStatus
{
    AwaitingPayment = 0,
    PaymentAuthorized = 1,
    Fulfilled = 2,
    Cancelled = 3,
    PartiallyRefunded = 4,
    Refunded = 5,
}
