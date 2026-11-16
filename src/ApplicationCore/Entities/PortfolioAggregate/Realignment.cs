using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment of every portfolio that follows a given model, back to the
/// model's current weights. Mirrored at Upvest as a portfolio-rebalancing execution.
/// </summary>
public class Realignment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Realignment() { }
#pragma warning restore CS8618

    public Realignment(Guid realignmentId, string modelId, Guid allocationId)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        RealignmentId = realignmentId;
        ModelId = modelId;
        UpvestAllocationId = allocationId;
        CreatedAt = DateTimeOffset.UtcNow;
        TriggerKey = Guid.NewGuid();
    }

    /// <summary>App-generated realignment identifier (route/response <c>realignmentId</c>). Unique.</summary>
    public Guid RealignmentId { get; private set; }

    public string ModelId { get; private set; }
    public Guid UpvestAllocationId { get; private set; }
    public Guid? UpvestExecutionId { get; private set; }
    public Guid TriggerKey { get; private set; }

    /// <summary>Last status learned from Upvest ("pending"/"completed"/"failed").</summary>
    public string StatusCache { get; private set; } = "pending";

    public DateTimeOffset CreatedAt { get; private set; }

    public void SetExecution(Guid executionId) => UpvestExecutionId = executionId;
    public void UpdateStatusCache(string status) => StatusCache = status;
}
