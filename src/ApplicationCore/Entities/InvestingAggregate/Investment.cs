using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of a shopper's accumulated spare change into the
/// configured fund, together with the Upvest order that carries it out and
/// the outcome of that order.
/// </summary>
public class Investment : BaseEntity
{
    public Guid PublicId { get; private set; } = Guid.NewGuid();

    /// <summary>The owning <see cref="Investor"/>.</summary>
    public int InvestorId { get; private set; }

    /// <summary>The euro amount invested.</summary>
    public decimal Amount { get; private set; }

    public InvestmentStatus Status { get; private set; } = InvestmentStatus.Pending;

    /// <summary>The identifier of the order placed at Upvest to carry out this investment.</summary>
    public string ProviderOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private Investment() { } // EF

    public Investment(decimal amount, string providerOrderId)
    {
        Amount = amount;
        ProviderOrderId = providerOrderId;
    }

    /// <summary>
    /// Moves the investment to its final outcome once it is known at Upvest.
    /// A no-op once already settled or failed.
    /// </summary>
    public void Settle() => MoveTo(InvestmentStatus.Settled);
    public void Fail() => MoveTo(InvestmentStatus.Failed);

    private void MoveTo(InvestmentStatus status)
    {
        if (Status == InvestmentStatus.Pending)
        {
            Status = status;
        }
    }
}
