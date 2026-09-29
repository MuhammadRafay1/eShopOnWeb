namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// The lifecycle state of an <see cref="Order"/> with respect to payment and fulfilment.
/// Kept deliberately free of any PayPal concepts - all PayPal-owned state lives on the
/// Payment aggregate, referenced by OrderId.
/// </summary>
public enum OrderStatus
{
    AwaitingPayment,
    PaymentAuthorized,
    Fulfilled,
    Cancelled,
    Refunded,
    PartiallyRefunded
}
