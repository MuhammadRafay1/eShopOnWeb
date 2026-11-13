using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in by an investor: an amount in euros credited to their Upvest account and invested
/// across the funds of the model they follow. Tracks the Upvest resources (top-up + portfolio
/// order) so the payment's current status can be read back from Upvest.
/// </summary>
public class PortfolioPayment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private PortfolioPayment() { }
#pragma warning restore CS8618

    public PortfolioPayment(string buyerId, decimal amount, string currency)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(currency, nameof(currency));
        BuyerId = buyerId;
        Amount = amount;
        Currency = currency;
        PublicId = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        Status = PortfolioStatuses.Pending;
    }

    /// <summary>Stable public identifier surfaced as <c>paymentId</c>.</summary>
    public Guid PublicId { get; private set; }

    public string BuyerId { get; private set; }

    /// <summary>Amount paid in, in euros.</summary>
    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    /// <summary>The Upvest top-up that credited the cash balance, if created.</summary>
    public string? UpvestTopupId { get; private set; }

    /// <summary>The Upvest portfolio order that invested the cash, if created.</summary>
    public string? UpvestOrderId { get; private set; }

    /// <summary>Last status read from Upvest: pending, invested or failed.</summary>
    public string Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void SetTopup(string topupId) => UpvestTopupId = Guard.Against.NullOrEmpty(topupId);

    public void SetOrder(string orderId) => UpvestOrderId = Guard.Against.NullOrEmpty(orderId);

    public void UpdateStatus(string status) => Status = Guard.Against.NullOrEmpty(status);
}
