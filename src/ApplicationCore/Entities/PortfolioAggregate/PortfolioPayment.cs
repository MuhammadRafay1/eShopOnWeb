using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in by an investor: money credited to their Upvest account and invested across their
/// model's funds. The authoritative status lives at Upvest (the portfolio order) and is read back
/// on demand; <see cref="UpvestPortfolioOrderId"/> is the link.
/// </summary>
public class PortfolioPayment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private PortfolioPayment() { }
#pragma warning restore CS8618

    public PortfolioPayment(int investorId, decimal amount, string currency)
    {
        InvestorId = Guard.Against.NegativeOrZero(investorId, nameof(investorId));
        Amount = Guard.Against.NegativeOrZero(amount, nameof(amount));
        Currency = Guard.Against.NullOrEmpty(currency, nameof(currency));
        PublicId = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The paymentId exposed to callers.</summary>
    public Guid PublicId { get; private set; }

    public int InvestorId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public Guid? UpvestVirtualCashIncreaseId { get; private set; }

    public Guid? UpvestPortfolioOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void SetVirtualCashIncrease(Guid id) => UpvestVirtualCashIncreaseId = id;

    public void SetPortfolioOrder(Guid id) => UpvestPortfolioOrderId = id;
}
