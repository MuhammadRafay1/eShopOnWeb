using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in the investor made: an amount in euros credited to their account and invested across their model.
/// Keyed by a shop-generated <see cref="PaymentId"/> written before any Upvest call, so a retried
/// submission is refused by the primary key. The authoritative status is re-derived from the Upvest
/// portfolio order on read.
/// </summary>
public class PaymentRecord : IAggregateRoot
{
    /// <summary>Shop-generated payment id. Primary key.</summary>
    public string PaymentId { get; private set; }

    /// <summary>Owning shopper. Payments are only ever visible to their owner.</summary>
    public string BuyerId { get; private set; }

    /// <summary>Amount paid in, in euros.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Last known status: Pending / Invested / Failed.</summary>
    public string Status { get; private set; } = PaymentStatus.Pending.ToString();

    /// <summary>Upvest top-up id for the sandbox cash-in, once placed.</summary>
    public Guid? UpvestTopupId { get; private set; }

    /// <summary>Upvest portfolio-order id that invests the cash, once placed. The payment's status tracks this order.</summary>
    public Guid? UpvestOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

#pragma warning disable CS8618 // Required by Entity Framework
    private PaymentRecord() { }
#pragma warning restore CS8618

    public PaymentRecord(string paymentId, string buyerId, decimal amount)
    {
        PaymentId = Guard.Against.NullOrEmpty(paymentId, nameof(paymentId));
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Amount = Guard.Against.NegativeOrZero(amount, nameof(amount));
    }

    public void SetTopup(Guid topupId) => UpvestTopupId = topupId;

    public void SetOrder(Guid orderId) => UpvestOrderId = orderId;

    public void SetStatus(PaymentStatus status) => Status = status.ToString();
}
