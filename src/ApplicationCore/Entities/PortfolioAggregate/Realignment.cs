using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment of every investor following a model back to its current weights.
/// Backed by an Upvest rebalancing execution; status is derived from that execution's child orders.
/// </summary>
public class Realignment : BaseEntity, IAggregateRoot
{
    public string ModelId { get; private set; }

    public string? UpvestExecutionId { get; private set; }

    /// <summary>Last-known derived status: pending / completed / failed.</summary>
    public RealignmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Realignment()
    {
        ModelId = default!;
    }

    public Realignment(string modelId)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        Status = RealignmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void SetExecution(string executionId)
    {
        UpvestExecutionId = Guard.Against.NullOrEmpty(executionId, nameof(executionId));
        Touch();
    }

    public void SetStatus(RealignmentStatus status)
    {
        Status = status;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
