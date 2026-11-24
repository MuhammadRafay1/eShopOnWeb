using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in by a shopper: an amount in euros credited to their Upvest account and invested across
/// the funds of the model they follow. Its status mirrors the Upvest portfolio order's outcome.
/// </summary>
public class PortfolioPayment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private PortfolioPayment() { }
#pragma warning restore CS8618

    public PortfolioPayment(string shopperId, decimal amount, string currency)
    {
        ShopperId = Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        Amount = Guard.Against.NegativeOrZero(amount, nameof(amount));
        Currency = Guard.Against.NullOrEmpty(currency, nameof(currency));
        PublicId = Guid.NewGuid().ToString();
        Status = PaymentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Stable public id returned to callers (the paymentId).</summary>
    public string PublicId { get; private set; }

    public string ShopperId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }

    /// <summary>The Upvest test bank transaction id that credited the cash, if performed.</summary>
    public string? UpvestBankTransactionId { get; private set; }

    /// <summary>The Upvest portfolio order id that invested the cash, if placed.</summary>
    public string? UpvestOrderId { get; private set; }

    public PaymentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void RecordBankTransaction(string bankTransactionId)
        => UpvestBankTransactionId = Guard.Against.NullOrEmpty(bankTransactionId, nameof(bankTransactionId));

    public void RecordOrder(string orderId)
        => UpvestOrderId = Guard.Against.NullOrEmpty(orderId, nameof(orderId));

    public void SetStatus(PaymentStatus status) => Status = status;
}
