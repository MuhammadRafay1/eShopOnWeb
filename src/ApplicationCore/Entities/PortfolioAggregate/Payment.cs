using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

public enum PaymentStatus
{
    /// <summary>The payment's outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The money has been invested across the model's funds.</summary>
    Invested = 1,

    /// <summary>The payment failed at Upvest.</summary>
    Failed = 2
}

/// <summary>
/// An amount a shopper paid into their managed portfolio, invested across their model's funds at Upvest.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
    /// <summary>Stable, externally exposed identifier (returned as <c>paymentId</c>).</summary>
    public Guid PublicId { get; private set; } = Guid.NewGuid();

    /// <summary>The owning shopper's identity; a payment is only ever visible to its owner.</summary>
    public string ShopperId { get; private set; }

    /// <summary>Amount in euros.</summary>
    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "EUR";

    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;

    /// <summary>The Upvest portfolio order that invests this payment, once placed.</summary>
    public string? UpvestOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public Payment(string shopperId, decimal amount)
    {
        ShopperId = Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        Amount = Guard.Against.NegativeOrZero(amount, nameof(amount));
    }

#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }
#pragma warning restore CS8618

    public void SetUpvestOrder(string orderId)
    {
        UpvestOrderId = Guard.Against.NullOrEmpty(orderId, nameof(orderId));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetStatus(PaymentStatus status)
    {
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
