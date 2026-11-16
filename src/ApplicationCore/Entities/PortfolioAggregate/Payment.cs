using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A shopper's pay-in. The shop generates the <see cref="PaymentId"/>; the authoritative
/// outcome lives at Upvest and is read back via <see cref="UpvestOrderId"/>. A payment whose
/// money was never invested (funding or order never reached Upvest) records a local
/// <see cref="LocalOutcome"/> instead.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }
#pragma warning restore CS8618

    public Payment(Guid paymentId, string shopUserId, decimal amount)
    {
        PaymentId = Guard.Against.Default(paymentId, nameof(paymentId));
        ShopUserId = Guard.Against.NullOrEmpty(shopUserId, nameof(shopUserId));
        Amount = Guard.Against.NegativeOrZero(amount, nameof(amount));
        CreatedAt = DateTimeOffset.UtcNow;
        LocalOutcome = PaymentLocalOutcome.Pending;
    }

    /// <summary>The shop-generated payment id returned to the caller. Unique.</summary>
    public Guid PaymentId { get; private set; }

    /// <summary>Owner identity (JWT). Scopes the payment to its shopper.</summary>
    public string ShopUserId { get; private set; }

    /// <summary>Amount paid in, in euros.</summary>
    public decimal Amount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The Upvest portfolio-order id once the BUY order has been placed.</summary>
    public Guid? UpvestOrderId { get; private set; }

    /// <summary>
    /// The locally-known outcome used only until an order exists at Upvest. Once
    /// <see cref="UpvestOrderId"/> is set, the true status is read from Upvest.
    /// </summary>
    public PaymentLocalOutcome LocalOutcome { get; private set; }

    public void SetOrder(Guid orderId)
    {
        UpvestOrderId = orderId;
        LocalOutcome = PaymentLocalOutcome.Pending;
    }

    /// <summary>Mark the pay-in as failed before any order reached Upvest.</summary>
    public void MarkFailed() => LocalOutcome = PaymentLocalOutcome.Failed;
}

/// <summary>The pay-in outcome the shop knows before an Upvest order exists.</summary>
public enum PaymentLocalOutcome
{
    /// <summary>Not yet invested; the outcome is still being settled.</summary>
    Pending = 0,

    /// <summary>The money was never invested (funding or order never reached Upvest).</summary>
    Failed = 1
}
