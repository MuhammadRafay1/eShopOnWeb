namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// Money-state lifecycle for a <see cref="Payment"/>. Mirrors the order lifecycle but tracks the
/// PayPal money movement specifically. Updated together with <see cref="OrderStatus"/> through the
/// entity transition methods so the two can never disagree.
/// </summary>
public enum PaymentStatus
{
    /// <summary>Payment record created; no PayPal order/authorization exists yet.</summary>
    Created = 0,

    /// <summary>Funds authorized (held) at PayPal.</summary>
    Authorized = 1,

    /// <summary>Funds captured (taken) at PayPal.</summary>
    Captured = 2,

    /// <summary>The authorization was voided; held funds released.</summary>
    Voided = 3,

    /// <summary>The capture has been partly refunded.</summary>
    PartiallyRefunded = 4,

    /// <summary>The capture has been refunded in full.</summary>
    Refunded = 5,

    /// <summary>A PayPal operation failed terminally (e.g. authorization declined, reauthorization impossible).</summary>
    Failed = 6
}
