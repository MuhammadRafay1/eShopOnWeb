namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

public enum PaymentStatus
{
    AwaitingPayment,
    Authorized,
    Captured,
    PartiallyRefunded,
    Refunded,
    Cancelled,
    Failed
}
