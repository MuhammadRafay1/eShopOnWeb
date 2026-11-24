using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

public enum PaymentStatus
{
    /// <summary>The payment's outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The money was invested across the model's funds at Upvest.</summary>
    Invested = 1,

    /// <summary>The payment failed at Upvest.</summary>
    Failed = 2
}

/// <summary>
/// A pay-in made by an investor: an amount in euros credited to their Upvest account and invested
/// across the funds of the model they follow, in that model's weights.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
    public string ShopperId { get; private set; }

    /// <summary>Identifier of the portfolio order Upvest created for this pay-in.</summary>
    public string? UpvestOrderId { get; private set; }

    /// <summary>Amount paid in, in euros.</summary>
    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public PaymentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

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

    public void SetUpvestOrder(string upvestOrderId)
    {
        Guard.Against.NullOrEmpty(upvestOrderId, nameof(upvestOrderId));
        UpvestOrderId = upvestOrderId;
    }

    public void UpdateStatus(PaymentStatus status) => Status = status;
}
