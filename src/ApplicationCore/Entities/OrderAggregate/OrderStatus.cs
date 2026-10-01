namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// The lifecycle state of an <see cref="Order"/> as it moves through payment, fulfilment,
/// cancellation and refund. This is the application's own state machine — it is distinct from, and
/// never confused with, any status PayPal reports (those are kept as raw strings on
/// <see cref="OrderPayment"/>).
/// </summary>
public enum OrderStatus
{
    /// <summary>Order placed; no money has been authorized yet.</summary>
    AwaitingPayment = 0,

    /// <summary>An authorization request is in flight (claim taken before calling PayPal).</summary>
    Authorizing = 1,

    /// <summary>Funds are held (authorized) but not captured.</summary>
    Authorized = 2,

    /// <summary>A capture request is in flight (claim taken before calling PayPal).</summary>
    Capturing = 3,

    /// <summary>The order was fulfilled and the held funds captured.</summary>
    Fulfilled = 4,

    /// <summary>A void request is in flight (claim taken before calling PayPal).</summary>
    Cancelling = 5,

    /// <summary>The order was cancelled; any hold was released, so no money moved.</summary>
    Cancelled = 6,

    /// <summary>Part of the captured amount has been refunded.</summary>
    PartiallyRefunded = 7,

    /// <summary>The whole captured amount has been refunded.</summary>
    Refunded = 8,

    /// <summary>
    /// A write to PayPal could not be confirmed (connection lost after the request may have been
    /// acted on). Needs a human/sweep to reconcile — never silently treated as success or failure.
    /// </summary>
    Unknown = 9
}
