namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// Lifecycle of an <see cref="Order"/> once payment is involved. Legacy orders created by the
/// storefront checkout flow simply start (and stay) at <see cref="AwaitingPayment"/>.
/// </summary>
public enum OrderStatus
{
    AwaitingPayment,
    PaymentAuthorized,
    Fulfilled,
    PartiallyRefunded,
    Refunded,
    Cancelled
}
