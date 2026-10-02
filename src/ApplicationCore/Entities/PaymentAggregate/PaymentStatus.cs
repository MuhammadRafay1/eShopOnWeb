namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// The lifecycle of an order's payment, independent of the catalog/basket flow.
/// </summary>
public enum PaymentStatus
{
    /// <summary>The order was placed but no money has been held yet.</summary>
    AwaitingPayment = 0,

    /// <summary>Funds are held (authorized) with PayPal, but not yet captured.</summary>
    Authorized = 1,

    /// <summary>The held funds were captured at fulfilment.</summary>
    Captured = 2,

    /// <summary>Part of the captured amount has been refunded.</summary>
    PartiallyRefunded = 3,

    /// <summary>The captured amount has been fully refunded.</summary>
    Refunded = 4,

    /// <summary>The authorization was voided before fulfilment; no money moved.</summary>
    Cancelled = 5,

    /// <summary>The authorization attempt failed and no hold exists.</summary>
    Failed = 6
}
