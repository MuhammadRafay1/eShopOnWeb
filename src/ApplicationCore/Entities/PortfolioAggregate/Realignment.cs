using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A realignment: bringing the portfolio of every investor who follows a model back to the
/// model's current weights, carried out at Upvest as a rebalancing execution.
/// </summary>
public class Realignment : BaseEntity, IAggregateRoot
{
    /// <summary>The model whose followers are being realigned.</summary>
    public int ModelId { get; private set; }

    /// <summary>The Upvest rebalancing execution id, once triggered.</summary>
    public string? UpvestExecutionId { get; private set; }

    public RealignmentStatus Status { get; private set; } = RealignmentStatus.Pending;

    /// <summary>Non-sensitive reason recorded when a realignment fails.</summary>
    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

#pragma warning disable CS8618 // Required by Entity Framework
    private Realignment() { }
#pragma warning restore CS8618

    public Realignment(int modelId)
    {
        ModelId = modelId;
    }

    public void SetExecution(string executionId)
        => UpvestExecutionId = Guard.Against.NullOrEmpty(executionId, nameof(executionId));

    public void UpdateStatus(RealignmentStatus status) => Status = status;

    public void MarkFailed(string reason)
    {
        Status = RealignmentStatus.Failed;
        FailureReason = reason;
    }
}
