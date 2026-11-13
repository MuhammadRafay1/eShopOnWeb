using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment of every portfolio that follows a model, back to the model's
/// current weights. Maps to a Upvest portfolio-rebalancing execution.
/// </summary>
public class Realignment : BaseEntity, IAggregateRoot
{
    /// <summary>App-generated id, surfaced to callers as <c>realignmentId</c>. Also used as the Upvest idempotency key.</summary>
    public Guid RealignmentId { get; private set; }

    public string ModelId { get; private set; }
    public Guid AllocationId { get; private set; }

    public Guid? UpvestExecutionId { get; private set; }

    public RealignmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private Realignment() { }
#pragma warning restore CS8618

    public Realignment(Guid realignmentId, string modelId, Guid allocationId)
    {
        RealignmentId = Guard.Against.Default(realignmentId, nameof(realignmentId));
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        AllocationId = Guard.Against.Default(allocationId, nameof(allocationId));
        Status = RealignmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetExecution(Guid executionId) => UpvestExecutionId = executionId;
    public void UpdateStatus(RealignmentStatus status) => Status = status;
}
