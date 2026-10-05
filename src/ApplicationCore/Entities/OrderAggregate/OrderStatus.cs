namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// Lifecycle of an order once money is involved. Orders placed before payments existed (and orders placed
/// through the storefront checkout) start, like every new order, as <see cref="AwaitingPayment"/>.
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
