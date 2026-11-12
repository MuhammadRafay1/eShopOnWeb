using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in by an investor: an amount in euros credited to their Upvest account and invested across the
/// funds of the model they follow. Keyed by an app-generated <see cref="PaymentId"/> written before the
/// Upvest calls, so a concurrent double-submit is refused by the store.
/// </summary>
public class Payment : IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }
#pragma warning restore CS8618

    public Payment(Guid paymentId, string buyerId, decimal amount, Guid idempotencyKey)
    {
        PaymentId = Guard.Against.Default(paymentId, nameof(paymentId));
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Amount = Guard.Against.NegativeOrZero(amount, nameof(amount));
        IdempotencyKey = idempotencyKey;
        Status = PaymentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>App-generated identifier (primary key).</summary>
    public Guid PaymentId { get; private set; }

    /// <summary>The paying shopper's identity (from the JWT). Scopes the payment to one shopper.</summary>
    public string BuyerId { get; private set; }

    /// <summary>Amount paid in, in euros.</summary>
    public decimal Amount { get; private set; }

    public PaymentStatus Status { get; private set; }

    /// <summary>The Upvest portfolio order that invests this payment, once placed.</summary>
    public Guid? UpvestOrderId { get; private set; }

    public Guid IdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void RecordOrder(Guid orderId) => UpvestOrderId = orderId;

    public void SetStatus(PaymentStatus status) => Status = status;
}
