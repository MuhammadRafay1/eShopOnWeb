using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of a shopper's set-aside change into the configured fund.
/// Part of the <see cref="InvestingAccount"/> aggregate; only mutated through it.
/// </summary>
public class Investment : BaseEntity
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }
#pragma warning restore CS8618

    internal Investment(decimal amount)
    {
        PublicId = Guid.NewGuid();
        Amount = amount;
        Status = InvestmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>Stable identifier exposed to API callers as <c>investmentId</c>.</summary>
    public Guid PublicId { get; private set; }

    /// <summary>The amount, in euros, that was invested.</summary>
    public decimal Amount { get; private set; }

    public InvestmentStatus Status { get; private set; }

    /// <summary>The identifier of the order Upvest placed on the shopper's behalf, once known.</summary>
    public string? UpvestOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal void AttachOrder(string upvestOrderId)
    {
        UpvestOrderId = upvestOrderId;
        Touch();
    }

    internal void Settle()
    {
        if (Status == InvestmentStatus.Pending)
        {
            Status = InvestmentStatus.Settled;
            Touch();
        }
    }

    internal bool Fail()
    {
        if (Status != InvestmentStatus.Pending)
        {
            return false;
        }

        Status = InvestmentStatus.Failed;
        Touch();
        return true;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
