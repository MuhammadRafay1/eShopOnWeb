using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of a shopper's accumulated spare change into the configured fund, placed at Upvest.
/// Created the moment the set-aside balance crosses the investment threshold; its status thereafter reflects
/// the real outcome of the order at Upvest.
/// </summary>
public class Investment : BaseEntity, IAggregateRoot
{
    private Investment()
    {
        // Required by EF.
        BuyerId = string.Empty;
    }

    public Investment(string buyerId, decimal amount, DateTimeOffset now)
    {
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Amount = Guard.Against.NegativeOrZero(amount, nameof(amount));
        Status = InvestmentStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Stable public identifier returned to callers as <c>investmentId</c>, and used as the
    /// Upvest order's client reference so an ambiguous placement can be reconciled.</summary>
    public Guid InvestmentId { get; private set; } = Guid.NewGuid();

    public string BuyerId { get; private set; }

    /// <summary>The amount invested (euros).</summary>
    public decimal Amount { get; private set; }

    public InvestmentStatus Status { get; private set; }

    /// <summary>The Upvest order id, once the placement is known to have reached Upvest.</summary>
    public Guid? UpvestOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void RecordUpvestOrder(Guid orderId, DateTimeOffset now)
    {
        UpvestOrderId = orderId;
        UpdatedAt = now;
    }

    public void SetStatus(InvestmentStatus status, DateTimeOffset now)
    {
        Status = status;
        UpdatedAt = now;
    }
}
