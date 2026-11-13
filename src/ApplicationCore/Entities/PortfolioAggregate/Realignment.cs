using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment of every portfolio that follows a model, back to the
/// model's current weights. The app-facing <see cref="RealignmentId"/> is the claim written
/// before the Upvest call; it maps onto the Upvest rebalancing execution the realignment runs as.
/// </summary>
public class Realignment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Realignment() { }
#pragma warning restore CS8618

    public Realignment(string modelId)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        RealignmentId = Guid.NewGuid();
        ModelId = modelId;
        Status = PortfolioStatuses.Realignment.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        IdempotencyKey = Guid.NewGuid();
    }

    /// <summary>The caller-facing <c>realignmentId</c>. Unique.</summary>
    public Guid RealignmentId { get; private set; }

    /// <summary>The app model id whose followers this realignment targets.</summary>
    public string ModelId { get; private set; }

    /// <summary>The Upvest rebalancing execution this realignment runs as. Null until triggered.</summary>
    public Guid? UpvestExecutionId { get; private set; }

    /// <summary>pending / completed / failed — derived from the execution's orders at Upvest.</summary>
    public string Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid IdempotencyKey { get; private set; }

    public void SetUpvestExecution(Guid executionId) => UpvestExecutionId = executionId;

    public void SetStatus(string status)
    {
        Guard.Against.NullOrEmpty(status, nameof(status));
        Status = status;
    }
}
