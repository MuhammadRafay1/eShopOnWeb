using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in by an investor: a cash amount (euros) credited at Upvest and invested across the funds
/// of the model the investor follows. Belongs to one shopper (<see cref="BuyerId"/>).
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
    /// <summary>App-generated id, surfaced to callers as <c>paymentId</c>. Also used as the Upvest idempotency key.</summary>
    public Guid PaymentId { get; private set; }

    public string BuyerId { get; private set; }
    public int InvestorId { get; private set; }

    /// <summary>Amount paid in, in euros.</summary>
    public decimal Amount { get; private set; }

    public Guid? UpvestTopUpId { get; private set; }
    public Guid? UpvestOrderId { get; private set; }

    /// <summary>Idempotency key for the portfolio BUY order (the top-up uses <see cref="PaymentId"/>).</summary>
    public Guid OrderIdempotencyKey { get; private set; }

    public PaymentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }
#pragma warning restore CS8618

    public Payment(Guid paymentId, string buyerId, int investorId, decimal amount)
    {
        PaymentId = Guard.Against.Default(paymentId, nameof(paymentId));
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        InvestorId = investorId;
        Amount = Guard.Against.NegativeOrZero(amount, nameof(amount));
        OrderIdempotencyKey = Guid.NewGuid();
        Status = PaymentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetTopUp(Guid topUpId) => UpvestTopUpId = topUpId;
    public void SetOrder(Guid orderId) => UpvestOrderId = orderId;
    public void UpdateStatus(PaymentStatus status) => Status = status;
}
