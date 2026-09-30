namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

public enum PaymentStatus
{
    AwaitingPayment,
    Authorized,
    AuthorizationFailed,
    Captured,
    Cancelled,
    PartiallyRefunded,
    Refunded
}
