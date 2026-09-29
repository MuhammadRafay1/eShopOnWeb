namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// Lifecycle of an <see cref="Order"/> once payment is part of the flow.
/// AwaitingPayment -> Authorized -> Fulfilled -> (PartiallyRefunded | Refunded)
/// AwaitingPayment/Authorized -> Cancelled (pre-fulfilment only).
/// </summary>
public enum OrderStatus
{
    AwaitingPayment = 0,
    Authorized = 1,
    Fulfilled = 2,
    Cancelled = 3,
    PartiallyRefunded = 4,
    Refunded = 5
}
