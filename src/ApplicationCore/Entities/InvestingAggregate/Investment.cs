using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment made on a shopper's behalf: the set-aside balance, once it reaches the
/// threshold, is invested in one exchange-traded fund at Upvest. One <see cref="Investment"/>
/// corresponds to one Upvest order.
/// </summary>
public class Investment : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }
    #pragma warning restore CS8618

    public Investment(decimal amount)
    {
        PublicId = Guid.NewGuid();
        Amount = amount;
        Status = InvestmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Stable identifier handed back to callers as <c>investmentId</c>.</summary>
    public Guid PublicId { get; private set; }

    /// <summary>Euro amount invested.</summary>
    public decimal Amount { get; private set; }

    public InvestmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The Upvest order id this investment was placed as, once known.</summary>
    public string? UpvestOrderId { get; private set; }

    public void LinkToUpvestOrder(string upvestOrderId)
    {
        UpvestOrderId = upvestOrderId;
    }

    public void MarkSettled()
    {
        Status = InvestmentStatus.Settled;
    }

    public void MarkFailed()
    {
        Status = InvestmentStatus.Failed;
    }
}
