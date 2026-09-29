namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

public enum PaymentStatus
{
    Authorized = 0,
    Captured = 1,
    Voided = 2,
    PartiallyRefunded = 3,
    Refunded = 4,
}
