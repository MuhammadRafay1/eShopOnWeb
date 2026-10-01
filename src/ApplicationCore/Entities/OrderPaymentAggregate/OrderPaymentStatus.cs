namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderPaymentAggregate;

public enum OrderPaymentStatus
{
    AwaitingPayment,
    Authorized,
    Captured,
    PartiallyRefunded,
    Refunded,
    Cancelled,
    Failed
}
