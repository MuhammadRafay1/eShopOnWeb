namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

public enum PaymentStatus
{
    AwaitingPayment,
    Authorizing,
    Authorized,
    AuthorizationFailed,
    Fulfilling,
    Captured,
    PartiallyRefunded,
    Refunded,
    Cancelled
}
