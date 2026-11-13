using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment that brings every investor following a model back to the
/// model's current weights. Mirrors an Upvest rebalancing execution; status is read from Upvest.
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
        PublicId = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        Status = PortfolioStatuses.Pending;
    }

    /// <summary>Stable public identifier surfaced as <c>realignmentId</c>.</summary>
    public Guid PublicId { get; private set; }

    public string ModelId { get; private set; }

    /// <summary>The Upvest rebalancing execution id, once created.</summary>
    public string? UpvestExecutionId { get; private set; }

    /// <summary>Last status read from Upvest: pending, completed or failed.</summary>
    public string Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void SetExecution(string executionId) => UpvestExecutionId = Guard.Against.NullOrEmpty(executionId);

    public void UpdateStatus(string status) => Status = Guard.Against.NullOrEmpty(status);
}
