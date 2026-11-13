using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment that brings every portfolio following a given model back to
/// the model's current weights, backed by an Upvest rebalancing execution.
/// </summary>
public class Realignment : BaseEntity, IAggregateRoot
{
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

    /// <summary>The model whose followers are being realigned.</summary>
    public string ModelId { get; private set; }

    /// <summary>The Upvest rebalancing execution id, once triggered.</summary>
    public string? UpvestExecutionId { get; private set; }

    public RealignmentStatus Status { get; private set; }
    public string? StatusDetail { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void LinkExecution(string executionId)
    {
        Guard.Against.NullOrEmpty(executionId, nameof(executionId));
        UpvestExecutionId = executionId;
    }

    public void SetStatus(RealignmentStatus status, string? detail = null)
    {
        Status = status;
        StatusDetail = detail;
    }
}
