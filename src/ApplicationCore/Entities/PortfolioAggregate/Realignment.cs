using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment: brings every investor who follows a model back to the
/// model's current weights. Its status mirrors the Upvest rebalancing execution's outcome.
/// </summary>
public class Realignment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Realignment() { }
#pragma warning restore CS8618

    public Realignment(string modelId)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        PublicId = Guid.NewGuid().ToString();
        Status = RealignmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Stable public id returned to callers (the route's realignmentId).</summary>
    public string PublicId { get; private set; }

    public string ModelId { get; private set; }

    /// <summary>The Upvest rebalancing execution id, once triggered.</summary>
    public string? UpvestExecutionId { get; private set; }

    public RealignmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void RecordExecution(string executionId)
        => UpvestExecutionId = Guard.Against.NullOrEmpty(executionId, nameof(executionId));

    public void SetStatus(RealignmentStatus status) => Status = status;
}
