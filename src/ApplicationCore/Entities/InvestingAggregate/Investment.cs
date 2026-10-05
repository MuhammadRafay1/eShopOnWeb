using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of accumulated set-aside change into the configured fund.
/// Child entity of the <see cref="Investor"/> aggregate.
/// </summary>
public class Investment : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }

    public Investment(long amountCents)
    {
        PublicId = Guid.NewGuid();
        AmountCents = amountCents;
        Status = InvestmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Stable identifier surfaced to API callers as <c>investmentId</c>.</summary>
    public Guid PublicId { get; private set; }

    public int InvestorId { get; private set; }

    /// <summary>Amount invested, in euro cents.</summary>
    public long AmountCents { get; private set; }

    public InvestmentStatus Status { get; private set; }

    /// <summary>The id of the buy order at Upvest, once placed.</summary>
    public string? UpvestOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void SetUpvestOrderId(string upvestOrderId) => UpvestOrderId = upvestOrderId;

    public void MarkSettled() => Status = InvestmentStatus.Settled;

    public void MarkFailed() => Status = InvestmentStatus.Failed;
}
