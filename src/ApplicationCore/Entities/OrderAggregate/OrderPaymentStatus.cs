namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

public enum OrderPaymentStatus
{
    AwaitingPayment,
    Authorized,
    Fulfilled,
    Cancelled,
    PartiallyRefunded,
    Refunded
}
