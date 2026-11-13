using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in by an investor: an amount in euros credited to their account at Upvest and invested
/// across the funds of the model they follow. Keyed by <see cref="PaymentId"/> and owned by the
/// shopper identified by <see cref="BuyerId"/>.
/// </summary>
public class PortfolioPayment : IAggregateRoot
{
    // Required by EF Core
#pragma warning disable CS8618
    private PortfolioPayment() { }
#pragma warning restore CS8618

    public PortfolioPayment(string buyerId, decimal amount)
    {
        PaymentId = Guid.NewGuid();
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Amount = Guard.Against.NegativeOrZero(amount, nameof(amount));
        Status = PaymentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;

        // Real idempotency keys, generated once and reused on resend so a replay returns the
        // original cash credit / order rather than duplicating it.
        CashCreditIdempotencyKey = Guid.NewGuid();
        PlaceOrderIdempotencyKey = Guid.NewGuid();
    }

    /// <summary>Application-owned id reported to the caller as <c>paymentId</c>. Primary key.</summary>
    public Guid PaymentId { get; private set; }

    /// <summary>The owning shopper's identity.</summary>
    public string BuyerId { get; private set; }

    /// <summary>Amount paid in, in euros.</summary>
    public decimal Amount { get; private set; }

    public PaymentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The Upvest order this payment was invested through, once placed.</summary>
    public Guid? UpvestOrderId { get; private set; }

    public bool CashCredited { get; private set; }

    public Guid CashCreditIdempotencyKey { get; private set; }
    public Guid PlaceOrderIdempotencyKey { get; private set; }

    public void MarkCashCredited() => CashCredited = true;
    public void SetUpvestOrderId(Guid orderId) => UpvestOrderId = orderId;

    public void MarkInvested() => Status = PaymentStatus.Invested;
    public void MarkFailed() => Status = PaymentStatus.Failed;
    public void MarkPending() => Status = PaymentStatus.Pending;
}
