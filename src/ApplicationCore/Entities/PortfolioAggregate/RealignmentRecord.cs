using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment of every portfolio that follows a model, back to the model's current
/// weights. Keyed by a shop-generated <see cref="RealignmentId"/> written before the Upvest call. The
/// authoritative status is re-derived from the Upvest rebalancing execution's orders on read.
/// </summary>
public class RealignmentRecord : IAggregateRoot
{
    /// <summary>Shop-generated realignment id. Primary key.</summary>
    public string RealignmentId { get; private set; }

    /// <summary>The shop model this realignment was triggered for.</summary>
    public string ModelId { get; private set; }

    /// <summary>Upvest rebalancing execution id, once triggered.</summary>
    public Guid? UpvestExecutionId { get; private set; }

    /// <summary>Last known status: Pending / Completed / Failed.</summary>
    public string Status { get; private set; } = RealignmentStatus.Pending.ToString();

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

#pragma warning disable CS8618 // Required by Entity Framework
    private RealignmentRecord() { }
#pragma warning restore CS8618

    public RealignmentRecord(string realignmentId, string modelId)
    {
        RealignmentId = Guard.Against.NullOrEmpty(realignmentId, nameof(realignmentId));
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
    }

    public void SetExecution(Guid executionId) => UpvestExecutionId = executionId;

    public void SetStatus(RealignmentStatus status) => Status = status.ToString();
}
