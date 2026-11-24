using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

public enum RealignmentStatus
{
    /// <summary>The outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>Every affected portfolio was brought back to the model's weights.</summary>
    Completed = 1,

    /// <summary>The realignment failed at Upvest.</summary>
    Failed = 2
}

/// <summary>
/// An operator-triggered realignment that brings the portfolio of every investor following a model
/// back to the model's current weights, backed by a rebalancing execution at Upvest.
/// </summary>
public class Realignment : BaseEntity, IAggregateRoot
{
    /// <summary>The operator-defined model whose followers are realigned.</summary>
    public string ModelId { get; private set; }

    /// <summary>Identifier of the rebalancing execution Upvest created.</summary>
    public string? UpvestExecutionId { get; private set; }

    public RealignmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private Realignment() { }
#pragma warning restore CS8618

    public Realignment(string modelId)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        ModelId = modelId;
        Status = RealignmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetUpvestExecution(string executionId)
    {
        Guard.Against.NullOrEmpty(executionId, nameof(executionId));
        UpvestExecutionId = executionId;
    }

    public void UpdateStatus(RealignmentStatus status) => Status = status;
}
