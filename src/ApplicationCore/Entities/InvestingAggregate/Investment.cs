using System;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single tranche of a shopper's set-aside change invested on their behalf in the configured fund.
/// Money is held in integer euro-cents to avoid rounding drift.
/// </summary>
public class Investment : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }

    public Investment(long amountInCents, Guid idempotencyKey)
    {
        Guard.Against.NegativeOrZero(amountInCents, nameof(amountInCents));
        Guard.Against.Default(idempotencyKey, nameof(idempotencyKey));

        AmountInCents = amountInCents;
        IdempotencyKey = idempotencyKey;
        Status = InvestmentStatus.Pending;
        CreatedDate = DateTimeOffset.UtcNow;
    }

    /// <summary>Amount invested, in euro-cents.</summary>
    public long AmountInCents { get; private set; }

    /// <summary>
    /// Stable key used as the Upvest order idempotency key, so a caller-level retry of the same
    /// investment is not placed twice. Generated once, before the first Upvest call.
    /// </summary>
    public Guid IdempotencyKey { get; private set; }

    /// <summary>The Upvest order id, once the order has been accepted.</summary>
    public Guid? UpvestOrderId { get; private set; }

    public InvestmentStatus Status { get; private set; }

    public DateTimeOffset CreatedDate { get; private set; }

    public void LinkToUpvestOrder(Guid upvestOrderId)
    {
        Guard.Against.Default(upvestOrderId, nameof(upvestOrderId));
        UpvestOrderId = upvestOrderId;
    }

    public void MarkSettled() => Status = InvestmentStatus.Settled;

    public void MarkFailed() => Status = InvestmentStatus.Failed;
}
