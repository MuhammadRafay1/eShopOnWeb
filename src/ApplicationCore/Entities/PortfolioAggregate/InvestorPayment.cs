using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A single pay-in made by an investor. <see cref="Id"/> is this application's own paymentId (stable and
/// returned to the caller immediately); <see cref="PortfolioOrderId"/> links it to the Upvest portfolio
/// order once placed, which is where the authoritative status is read back from.
/// </summary>
public class InvestorPayment
{
    private InvestorPayment() { BuyerId = string.Empty; }

    public InvestorPayment(string buyerId, decimal amount, Guid idempotencyKey)
    {
        BuyerId = buyerId;
        Amount = amount;
        IdempotencyKey = idempotencyKey;
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string BuyerId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The real idempotency key sent to Upvest for the portfolio order (persisted before the call).</summary>
    public Guid IdempotencyKey { get; private set; }

    /// <summary>The Upvest portfolio-order id, set once the order has been placed.</summary>
    public Guid? PortfolioOrderId { get; set; }
}
