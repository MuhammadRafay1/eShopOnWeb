namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// Mirrors PayPal's own authorization_status enum exactly. There is deliberately no EXPIRED
/// member - PayPal itself does not report one; staleness is a derived fact
/// (<see cref="Payment.AuthorizationExpiresAt"/> &lt; now), not a status.
/// </summary>
public enum PaymentAuthorizationStatus
{
    Created,
    Captured,
    Denied,
    PartiallyCaptured,
    Voided,
    Pending
}

/// <summary>
/// Mirrors PayPal's own capture status enum.
/// </summary>
public enum PaymentCaptureStatus
{
    Completed,
    Declined,
    PartiallyRefunded,
    Pending,
    Refunded,
    Failed
}
