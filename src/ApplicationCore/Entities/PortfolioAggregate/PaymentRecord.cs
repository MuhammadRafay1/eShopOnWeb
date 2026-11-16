using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The application's record of a pay-in. The application owns the payment id (Upvest has no first-class
/// "payment" object); the id doubles as the deterministic idempotency key for the Upvest portfolio order,
/// so an SDK-level resend reaches Upvest at most once.
/// </summary>
public class PaymentRecord
{
    /// <summary>The application payment id (and the order's idempotency key). Primary key.</summary>
    public Guid PaymentId { get; set; }

    /// <summary>The shopper who paid in (JWT name claim) — scopes the payment to its owner.</summary>
    public string Shopper { get; set; } = default!;

    /// <summary>The amount paid in, in euros.</summary>
    public decimal Amount { get; set; }

    /// <summary>The last known status. GET always re-reads the order at Upvest and refreshes this.</summary>
    public PaymentStatus Status { get; set; }

    /// <summary>The Upvest portfolio-order id once placed (used to re-read the outcome).</summary>
    public Guid? UpvestOrderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
