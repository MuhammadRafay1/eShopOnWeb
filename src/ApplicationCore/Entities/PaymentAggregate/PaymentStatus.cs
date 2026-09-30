namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

public enum PaymentStatus
{
    Authorized,
    Captured,
    Voided,
    PartiallyRefunded,
    Refunded
}
