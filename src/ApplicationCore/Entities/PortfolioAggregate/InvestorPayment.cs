using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in made by an investor: an amount in euros credited to their Upvest account and
/// invested across the funds of the model they follow, in that model's weights.
/// </summary>
public class InvestorPayment : BaseEntity, IAggregateRoot
{
    /// <summary>The shopper who made the pay-in.</summary>
    public string ShopperId { get; private set; }

    /// <summary>Amount paid in, in euros.</summary>
    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "EUR";

    /// <summary>The Upvest portfolio order that invests this pay-in, once placed.</summary>
    public string? UpvestPortfolioOrderId { get; private set; }

    /// <summary>The Upvest top-up that credits the cash for this pay-in, once created.</summary>
    public string? UpvestTopupId { get; private set; }

    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;

    /// <summary>Non-sensitive reason recorded when a pay-in fails.</summary>
    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

#pragma warning disable CS8618 // Required by Entity Framework
    private InvestorPayment() { }
#pragma warning restore CS8618

    public InvestorPayment(string shopperId, decimal amount, string currency = "EUR")
    {
        ShopperId = Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        Amount = Guard.Against.NegativeOrZero(amount, nameof(amount));
        Currency = Guard.Against.NullOrEmpty(currency, nameof(currency));
    }

    public void SetTopup(string topupId) => UpvestTopupId = topupId;

    public void SetPortfolioOrder(string orderId) => UpvestPortfolioOrderId = orderId;

    public void UpdateStatus(PaymentStatus status) => Status = status;

    public void MarkFailed(string reason)
    {
        Status = PaymentStatus.Failed;
        FailureReason = reason;
    }
}
