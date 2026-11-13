using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in by an investor: an amount in euros credited to their Upvest account and invested
/// across the funds of the model they follow. Keyed to the shopper and to the Upvest order the
/// investment is placed as. The app-facing <see cref="PaymentId"/> is the claim written before
/// the Upvest call, so a second submission is refused by its unique key.
/// </summary>
public class PortfolioPayment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private PortfolioPayment() { }
#pragma warning restore CS8618

    public PortfolioPayment(string shopperUserName, decimal amount, string currency)
    {
        Guard.Against.NullOrEmpty(shopperUserName, nameof(shopperUserName));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(currency, nameof(currency));

        PaymentId = Guid.NewGuid();
        ShopperUserName = shopperUserName;
        Amount = amount;
        Currency = currency;
        Status = PortfolioStatuses.Payment.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        OrderIdempotencyKey = Guid.NewGuid();
    }

    /// <summary>The caller-facing <c>paymentId</c>. Unique.</summary>
    public Guid PaymentId { get; private set; }

    public string ShopperUserName { get; private set; }

    /// <summary>The amount paid in, in euros.</summary>
    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    /// <summary>The Upvest portfolio order this payment is invested as. Null until the order is placed.</summary>
    public Guid? UpvestOrderId { get; private set; }

    /// <summary>pending / invested / failed — reflects the order's outcome at Upvest.</summary>
    public string Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid OrderIdempotencyKey { get; private set; }

    public void SetUpvestOrder(Guid orderId) => UpvestOrderId = orderId;

    public void SetStatus(string status)
    {
        Guard.Against.NullOrEmpty(status, nameof(status));
        Status = status;
    }
}
