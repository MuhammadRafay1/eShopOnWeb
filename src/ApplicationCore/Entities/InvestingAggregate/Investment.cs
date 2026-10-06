using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of a shopper's accumulated set-aside change into the configured fund, backed by a
/// buy order at Upvest. Its status mirrors the order's outcome at Upvest.
/// </summary>
public class Investment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }
#pragma warning restore CS8618

    public Investment(int investorId, long amountCents)
    {
        InvestorId = investorId;
        AmountCents = amountCents;
        Status = InvestmentStatus.Pending;
        Reference = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Owning <see cref="Investor"/>. An investment belongs to that shopper alone.</summary>
    public int InvestorId { get; private set; }

    /// <summary>Amount invested, in euro cents.</summary>
    public long AmountCents { get; private set; }

    public InvestmentStatus Status { get; private set; }

    /// <summary>
    /// Stable client reference, used as the order's <c>client_reference</c> and idempotency key so a retry
    /// never double-invests and an unknown-outcome order can be found again.
    /// </summary>
    public Guid Reference { get; private set; }

    /// <summary>The Upvest order id, once the order has been placed.</summary>
    public Guid? UpvestOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void AttachOrder(Guid orderId) => UpvestOrderId = orderId;

    public void MarkSettled() => Status = InvestmentStatus.Settled;

    public void MarkFailed() => Status = InvestmentStatus.Failed;
}
