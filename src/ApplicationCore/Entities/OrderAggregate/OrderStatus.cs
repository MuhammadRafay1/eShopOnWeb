namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// Order-level lifecycle for the (additive) paid-order flow. This is the eShop domain's own
/// status and is never confused with PayPal's wire statuses (which never leave Infrastructure).
/// </summary>
public enum OrderStatus
{
    /// <summary>Order has been placed but no money has been held yet.</summary>
    AwaitingPayment = 0,

    /// <summary>Funds have been authorized (held) at PayPal but not captured.</summary>
    Authorized = 1,

    /// <summary>Order has been fulfilled and the captured funds taken.</summary>
    Fulfilled = 2,

    /// <summary>Order was cancelled before fulfilment; any held funds were released.</summary>
    Cancelled = 3,

    /// <summary>A captured order that has been partly refunded.</summary>
    PartiallyRefunded = 4,

    /// <summary>A captured order that has been refunded in full.</summary>
    Refunded = 5,

    /// <summary>A payment attempt failed and the order could not be authorized.</summary>
    PaymentFailed = 6
}
