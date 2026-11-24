using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment of every investor following a model back to its current
/// weights. Mirrors a rebalancing execution at Upvest; status is read back from Upvest on demand.
/// </summary>
public class PortfolioRealignment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private PortfolioRealignment() { }
#pragma warning restore CS8618

    public PortfolioRealignment(string modelId, Guid upvestExecutionId)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        UpvestExecutionId = Guard.Against.Default(upvestExecutionId, nameof(upvestExecutionId));
        PublicId = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The realignmentId exposed to callers.</summary>
    public Guid PublicId { get; private set; }

    public string ModelId { get; private set; }

    public Guid UpvestExecutionId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
