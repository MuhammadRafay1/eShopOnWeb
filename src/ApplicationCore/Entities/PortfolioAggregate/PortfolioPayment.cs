using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in by an investor: an amount in euros credited to their Upvest account and invested across
/// the funds of the model they follow. Tracks the Upvest top-up and portfolio order used to realize it,
/// and the idempotency keys so the three-step pay-in can be replayed safely.
/// </summary>
public class PortfolioPayment : BaseEntity, IAggregateRoot
{
    /// <summary>The caller-facing payment identifier, chosen by the app before any Upvest call.</summary>
    public Guid PaymentKey { get; private set; }

    /// <summary>The shopper this payment belongs to; a shopper never sees another's payments.</summary>
    public string ShopperUserName { get; private set; } = default!;

    public decimal Amount { get; private set; }

    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;

    public Guid? UpvestTopupId { get; private set; }
    public Guid? UpvestOrderId { get; private set; }

    public Guid TopupIdempotencyKey { get; private set; }
    public Guid BankTransactionIdempotencyKey { get; private set; }
    public Guid OrderIdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private PortfolioPayment() { } // EF

    public PortfolioPayment(string shopperUserName, decimal amount)
    {
        PaymentKey = Guid.NewGuid();
        ShopperUserName = shopperUserName;
        Amount = amount;
        Status = PaymentStatus.Pending;
        TopupIdempotencyKey = Guid.NewGuid();
        BankTransactionIdempotencyKey = Guid.NewGuid();
        OrderIdempotencyKey = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetTopup(Guid topupId) => UpvestTopupId = topupId;
    public void SetOrder(Guid orderId) => UpvestOrderId = orderId;
    public void SetStatus(PaymentStatus status) => Status = status;

    /// <summary>A stable client reference for the Upvest sandbox bank transaction that settles this payment.</summary>
    public string ClientReference => $"eshop-payment-{PaymentKey:N}";
}
