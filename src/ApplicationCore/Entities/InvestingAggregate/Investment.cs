using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of a shopper's set-aside balance into the configured fund. Carries stable
/// idempotency keys so the provider funding and order calls can be safely retried, and tracks the
/// provider-side order whose outcome this investment's status mirrors.
/// </summary>
public class Investment : BaseEntity, IAggregateRoot
{
    private Investment() { } // EF

    public Investment(string shopperId, decimal amount)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        ShopperId = shopperId;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Status = InvestmentStatus.Pending;
        TopUpIdempotencyKey = Guid.NewGuid();
        OrderIdempotencyKey = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string ShopperId { get; private set; } = default!;

    /// <summary>Amount invested, in euros.</summary>
    public decimal Amount { get; private set; }

    public InvestmentStatus Status { get; private set; }

    public Guid? UpvestOrderId { get; private set; }
    public bool ToppedUp { get; private set; }

    public Guid TopUpIdempotencyKey { get; private set; }
    public Guid OrderIdempotencyKey { get; private set; }

    /// <summary>How many times the provider calls for this investment have been attempted, to bound retries.</summary>
    public int Attempts { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public void RecordAttempt()
    {
        Attempts++;
        Touch();
    }

    public void MarkToppedUp()
    {
        ToppedUp = true;
        Touch();
    }

    public void SetOrderPlaced(Guid upvestOrderId)
    {
        UpvestOrderId = upvestOrderId;
        Touch();
    }

    public void MarkSettled()
    {
        Status = InvestmentStatus.Settled;
        Touch();
    }

    public void MarkFailed()
    {
        Status = InvestmentStatus.Failed;
        Touch();
    }
}
