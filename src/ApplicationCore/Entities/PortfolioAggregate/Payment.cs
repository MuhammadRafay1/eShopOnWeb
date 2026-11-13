using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in made by a shopper: money credited to their Upvest account and invested across their
/// model's funds. Scoped to a single shopper via <see cref="ShopperId"/>.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }
#pragma warning restore CS8618

    public Payment(string shopperId, decimal amount, string currency)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(currency, nameof(currency));
        ShopperId = shopperId;
        Amount = amount;
        Currency = currency;
        Status = PaymentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string ShopperId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public PaymentStatus Status { get; private set; }

    /// <summary>The Upvest top-up that credited the cash balance, once created.</summary>
    public string? UpvestTopupId { get; private set; }

    /// <summary>The Upvest portfolio order that invested the money, once created.</summary>
    public string? UpvestPortfolioOrderId { get; private set; }

    public string? StatusDetail { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void LinkTopup(string topupId) => UpvestTopupId = topupId;
    public void LinkPortfolioOrder(string orderId) => UpvestPortfolioOrderId = orderId;

    public void SetStatus(PaymentStatus status, string? detail = null)
    {
        Status = status;
        StatusDetail = detail;
    }
}
