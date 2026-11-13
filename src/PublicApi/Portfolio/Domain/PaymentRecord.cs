using System;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Domain;

/// <summary>
/// A shopper's pay-in: cash credited to their Upvest account (top-up) and invested across their model's funds
/// (portfolio order). Belongs to one shopper; only that shopper can see it.
/// </summary>
public class PaymentRecord
{
    /// <summary>Client-generated payment id surfaced to the caller as <c>paymentId</c>. Primary key.</summary>
    public Guid PaymentId { get; set; }

    /// <summary>The owning shopper (eShop identity).</summary>
    public string ShopperId { get; set; } = string.Empty;

    /// <summary>Amount paid in, in euros (two decimal places).</summary>
    public decimal Amount { get; set; }

    /// <summary>Reconciled status at Upvest.</summary>
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>Upvest top-up id, once the cash-in has been created.</summary>
    public Guid? UpvestTopupId { get; set; }

    /// <summary>Upvest portfolio-order id, once the invest order has been placed.</summary>
    public Guid? UpvestOrderId { get; set; }

    // Persisted idempotency keys so a retry reuses the same key per provider write.
    public Guid TopupIdempotencyKey { get; set; } = Guid.NewGuid();
    public Guid OrderIdempotencyKey { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
