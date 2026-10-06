using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single tranche of a shopper's set-aside change that has been invested on their behalf in the
/// configured exchange-traded fund. Owned by, and only reachable through, its <see cref="Investor"/>.
/// </summary>
public class Investment : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }
    #pragma warning restore CS8618

    public Investment(decimal amount, string clientReference, Guid idempotencyKey)
    {
        PublicId = Guid.NewGuid();
        Amount = amount;
        ClientReference = clientReference;
        IdempotencyKey = idempotencyKey;
        Status = InvestmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The identifier surfaced to callers as <c>investmentId</c>.</summary>
    public Guid PublicId { get; private set; }

    /// <summary>Amount invested, in euros.</summary>
    public decimal Amount { get; private set; }

    public InvestmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Stable, caller-derived reference sent to Upvest as the order's <c>client_reference</c> so the
    /// write can be reconciled by lookup if its outcome is ever unknown.
    /// </summary>
    public string ClientReference { get; private set; }

    /// <summary>Stable idempotency key reused across retries of this investment's provider writes.</summary>
    public Guid IdempotencyKey { get; private set; }

    /// <summary>The Upvest order id, once <c>PlaceOrder</c> has returned one.</summary>
    public Guid? UpvestOrderId { get; private set; }

    public void RecordUpvestOrder(Guid upvestOrderId) => UpvestOrderId = upvestOrderId;

    public void MarkSettled() => Status = InvestmentStatus.Settled;

    public void MarkFailed() => Status = InvestmentStatus.Failed;
}
