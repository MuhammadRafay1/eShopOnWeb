using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of a shopper's set-aside change into the configured fund at Upvest.
/// </summary>
public class Investment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }
#pragma warning restore CS8618

    public Investment(string buyerId, decimal amount)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        BuyerId = buyerId;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Status = InvestmentStatus.Pending;
        InvestmentId = Guid.NewGuid();
        CreatedDate = DateTimeOffset.UtcNow;
    }

    /// <summary>Stable external identifier, surfaced to callers as <c>investmentId</c>.</summary>
    public Guid InvestmentId { get; private set; }

    /// <summary>The shop's identity for the shopper this investment belongs to.</summary>
    public string BuyerId { get; private set; }

    /// <summary>Amount invested, in euros.</summary>
    public decimal Amount { get; private set; }

    public InvestmentStatus Status { get; private set; }

    /// <summary>The Upvest order placed for this investment.</summary>
    public Guid? UpvestOrderId { get; private set; }

    public DateTimeOffset CreatedDate { get; private set; }

    public void SetUpvestOrder(Guid orderId) => UpvestOrderId = orderId;

    public void SetStatus(InvestmentStatus status) => Status = status;
}
