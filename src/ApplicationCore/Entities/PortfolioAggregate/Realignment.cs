using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A realignment: bringing the portfolio of every investor who follows a model back to the model's current
/// weights. Keyed by an app-generated <see cref="RealignmentId"/> written before the Upvest trigger, so a
/// concurrent double-submit is refused by the store.
/// </summary>
public class Realignment : IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Realignment() { }
#pragma warning restore CS8618

    public Realignment(Guid realignmentId, string modelId, Guid idempotencyKey)
    {
        RealignmentId = Guard.Against.Default(realignmentId, nameof(realignmentId));
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        IdempotencyKey = idempotencyKey;
        Status = RealignmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>App-generated identifier (primary key).</summary>
    public Guid RealignmentId { get; private set; }

    /// <summary>The model being realigned.</summary>
    public string ModelId { get; private set; }

    public RealignmentStatus Status { get; private set; }

    /// <summary>The Upvest rebalancing-execution id, once triggered.</summary>
    public Guid? UpvestExecutionId { get; private set; }

    public Guid IdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void RecordExecution(Guid executionId) => UpvestExecutionId = executionId;

    public void SetStatus(RealignmentStatus status) => Status = status;
}
