using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in by an investor: an amount in euros credited to their Upvest account and invested across
/// the funds of their chosen model. Holds the correlation to the Upvest top-up and portfolio order,
/// the idempotency keys that make those writes safe to retry, and a cached status.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }
#pragma warning restore CS8618

    public Payment(Guid paymentId, string shopperId, decimal amount)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        PaymentId = paymentId;
        ShopperId = shopperId;
        Amount = amount;
        CreatedAt = DateTimeOffset.UtcNow;
        // A deterministic client reference lets us reconcile the sandbox funding transfer and the
        // top-up by reference if a connection fails after the provider may have acted.
        ClientReference = "ESHOP-" + paymentId.ToString("N");
        TopupKey = Guid.NewGuid();
        BankTransferKey = Guid.NewGuid();
        OrderKey = Guid.NewGuid();
    }

    /// <summary>App-generated payment identifier (route/response <c>paymentId</c>). Unique.</summary>
    public Guid PaymentId { get; private set; }

    /// <summary>Owning shopper (JWT identity name) — only this shopper may see the payment.</summary>
    public string ShopperId { get; private set; }

    public decimal Amount { get; private set; }

    public Guid? UpvestTopupId { get; private set; }
    public Guid? UpvestOrderId { get; private set; }

    public string ClientReference { get; private set; }

    public Guid TopupKey { get; private set; }
    public Guid BankTransferKey { get; private set; }
    public Guid OrderKey { get; private set; }

    /// <summary>Last status learned from Upvest ("pending"/"invested"/"failed").</summary>
    public string StatusCache { get; private set; } = "pending";

    public DateTimeOffset CreatedAt { get; private set; }

    public void SetTopup(Guid topupId) => UpvestTopupId = topupId;
    public void SetOrder(Guid orderId) => UpvestOrderId = orderId;
    public void UpdateStatusCache(string status) => StatusCache = status;
}
