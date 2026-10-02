namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

public enum OrderStatus
{
    AwaitingPayment,
    Authorizing,
    Authorized,
    AuthorizationFailed,
    Capturing,
    Fulfilled,
    FulfilmentFailed,
    Cancelling,
    Cancelled,
    Refunding,
    Refunded,
    PartiallyRefunded
}
