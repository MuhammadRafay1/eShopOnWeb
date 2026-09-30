namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

public enum PaymentStatus
{
    AwaitingPayment,
    Authorized,
    Fulfilled,
    Canceled,
    PartiallyRefunded,
    Refunded
}
