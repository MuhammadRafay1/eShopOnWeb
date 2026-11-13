using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.ManagedPortfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A shopper's pay-in. <see cref="PaymentId"/> is this application's own stable reference (the primary key,
/// returned to the caller, and reused as the Upvest idempotency key so a resend deduplicates). A payment
/// belongs to exactly one shopper.
/// </summary>
public class PortfolioPayment : IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private PortfolioPayment() { }
#pragma warning restore CS8618

    public PortfolioPayment(Guid paymentId, string shopperKey, decimal amount)
    {
        PaymentId = Guard.Against.Default(paymentId, nameof(paymentId));
        ShopperKey = Guard.Against.NullOrEmpty(shopperKey, nameof(shopperKey));
        Amount = Guard.Against.NegativeOrZero(amount, nameof(amount));
        CreatedAt = DateTimeOffset.UtcNow;
        Status = PaymentStatus.Pending;
    }

    public Guid PaymentId { get; private set; }
    public string ShopperKey { get; private set; }
    public decimal Amount { get; private set; }

    public Guid? UpvestBankTransactionId { get; private set; }
    public Guid? UpvestOrderId { get; private set; }

    public PaymentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void RecordFunding(Guid? bankTransactionId)
    {
        if (bankTransactionId.HasValue) UpvestBankTransactionId = bankTransactionId;
    }

    public void RecordOrder(Guid? orderId, PaymentStatus status)
    {
        if (orderId.HasValue) UpvestOrderId = orderId;
        Status = status;
    }

    public void UpdateStatus(PaymentStatus status) => Status = status;
}
