using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

public enum RealignmentStatus
{
    /// <summary>The realignment's outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>Upvest has realigned the followers' portfolios to the model's current weights.</summary>
    Completed = 1,

    /// <summary>The realignment failed at Upvest.</summary>
    Failed = 2
}

/// <summary>
/// An operator-triggered realignment that brings every investor following a model back to the
/// model's current weights. Mirrored at Upvest as a portfolio rebalancing execution.
/// </summary>
public class Realignment : BaseEntity, IAggregateRoot
{
    /// <summary>Stable, externally exposed identifier (returned as <c>realignmentId</c>).</summary>
    public Guid PublicId { get; private set; } = Guid.NewGuid();

    /// <summary>The model whose followers are being realigned.</summary>
    public int ModelId { get; private set; }

    public RealignmentStatus Status { get; private set; } = RealignmentStatus.Pending;

    /// <summary>The Upvest rebalancing execution, once triggered.</summary>
    public string? UpvestExecutionId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public Realignment(int modelId)
    {
        ModelId = modelId;
    }

#pragma warning disable CS8618 // Required by Entity Framework
    private Realignment() { }
#pragma warning restore CS8618

    public void SetUpvestExecution(string executionId)
    {
        UpvestExecutionId = Guard.Against.NullOrEmpty(executionId, nameof(executionId));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetStatus(RealignmentStatus status)
    {
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
