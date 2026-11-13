using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment that brings every investor following a model back to the
/// model's current weights. Keyed by <see cref="RealignmentId"/>.
/// </summary>
public class PortfolioRealignment : IAggregateRoot
{
    // Required by EF Core
#pragma warning disable CS8618
    private PortfolioRealignment() { }
#pragma warning restore CS8618

    public PortfolioRealignment(string modelId)
    {
        RealignmentId = Guid.NewGuid();
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        Status = RealignmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        TriggerIdempotencyKey = Guid.NewGuid();
    }

    /// <summary>Application-owned id reported to the caller as <c>realignmentId</c>. Primary key.</summary>
    public Guid RealignmentId { get; private set; }

    public string ModelId { get; private set; }

    public RealignmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The Upvest rebalancing execution this realignment was triggered as, once started.</summary>
    public Guid? UpvestExecutionId { get; private set; }

    public Guid TriggerIdempotencyKey { get; private set; }

    public void SetUpvestExecutionId(Guid executionId) => UpvestExecutionId = executionId;

    public void MarkCompleted() => Status = RealignmentStatus.Completed;
    public void MarkFailed() => Status = RealignmentStatus.Failed;
    public void MarkPending() => Status = RealignmentStatus.Pending;
}
