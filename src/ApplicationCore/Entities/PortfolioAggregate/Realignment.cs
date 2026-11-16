using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment of every portfolio that follows a given model, back to that model's
/// current weights. The <see cref="RealignmentId"/> is the Upvest rebalancing-execution id reported to callers;
/// its status is always derived from Upvest read-back.
/// </summary>
public class Realignment : IAggregateRoot
{
    private Realignment()
    {
        // Required by EF Core.
        ModelId = string.Empty;
    }

    public Realignment(Guid realignmentId, string modelId, Guid allocationId)
    {
        RealignmentId = realignmentId;
        ModelId = !string.IsNullOrWhiteSpace(modelId)
            ? modelId
            : throw new ArgumentException("Model id is required.", nameof(modelId));
        AllocationId = allocationId;
        Status = RealignmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Primary key: the Upvest rebalancing-execution id.</summary>
    public Guid RealignmentId { get; private set; }

    public string ModelId { get; private set; }

    public Guid AllocationId { get; private set; }

    public RealignmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void SetStatus(RealignmentStatus status) => Status = status;
}
