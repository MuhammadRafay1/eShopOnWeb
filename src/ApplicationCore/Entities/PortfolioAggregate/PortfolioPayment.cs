using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in made by an investor: a euro amount credited to their Upvest account and invested across the funds
/// of the model they follow.
/// <para>
/// Keyed by <see cref="PaymentId"/>, which doubles as the duplicate-submit claim: when the caller supplies an
/// idempotency key it becomes the primary key, so a re-submit is rejected by the store on insert and the
/// original payment is returned rather than a second order being placed.
/// </para>
/// </summary>
public class PortfolioPayment : IAggregateRoot
{
    private PortfolioPayment()
    {
        // Required by EF Core.
        ShopperUserName = string.Empty;
        Currency = "EUR";
    }

    public PortfolioPayment(Guid paymentId, string shopperUserName, decimal amount, string currency = "EUR")
    {
        PaymentId = paymentId;
        ShopperUserName = !string.IsNullOrWhiteSpace(shopperUserName)
            ? shopperUserName
            : throw new ArgumentException("Shopper user name is required.", nameof(shopperUserName));
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        Amount = amount;
        Currency = currency;
        Status = PaymentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        CashCreditIdempotencyKey = Guid.NewGuid();
        OrderIdempotencyKey = Guid.NewGuid();
    }

    /// <summary>Primary key / claim key. Equals the caller's idempotency key when one was supplied.</summary>
    public Guid PaymentId { get; private set; }

    /// <summary>Owning shopper — payments are only ever read back for their own owner.</summary>
    public string ShopperUserName { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public PaymentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The Upvest portfolio order id, once placed. Used to read the payment's status back.</summary>
    public Guid? UpvestOrderId { get; private set; }

    // Persisted idempotency keys for the two Upvest writes a pay-in performs (credit, then buy order).
    public Guid CashCreditIdempotencyKey { get; private set; }
    public Guid OrderIdempotencyKey { get; private set; }

    public void SetUpvestOrderId(Guid orderId) => UpvestOrderId = orderId;

    public void SetStatus(PaymentStatus status) => Status = status;
}
