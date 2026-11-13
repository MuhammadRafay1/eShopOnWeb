using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The lifecycle of a realignment, as reflected by Upvest. <see cref="Pending"/> until the outcome
/// at Upvest is known, then <see cref="Completed"/> or <see cref="Failed"/>.
/// </summary>
public enum RealignmentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2
}

/// <summary>
/// An operator-triggered realignment that brings every follower of a model back to the model's
/// current weights. Keyed by the application-generated <see cref="RealignmentId"/>; written as a
/// claim before the Upvest rebalancing call.
/// </summary>
public class Realignment : IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Realignment() { }
#pragma warning restore CS8618

    public Realignment(Guid realignmentId, string modelId)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        RealignmentId = realignmentId;
        ModelId = modelId;
        Status = RealignmentStatus.Pending;
        CreatedDate = DateTimeOffset.UtcNow;
        IdempotencyKey = Guid.NewGuid();
    }

    /// <summary>Application-facing realignment id. Primary key.</summary>
    public Guid RealignmentId { get; private set; }

    /// <summary>The model whose followers are being realigned.</summary>
    public string ModelId { get; private set; }

    public RealignmentStatus Status { get; private set; }

    /// <summary>The Upvest rebalancing-execution id.</summary>
    public Guid? UpvestExecutionId { get; private set; }

    public Guid IdempotencyKey { get; private set; }

    public DateTimeOffset CreatedDate { get; private set; }

    public string? Note { get; private set; }

    public void SetExecutionId(Guid executionId) => UpvestExecutionId = executionId;
    public void SetStatus(RealignmentStatus status) => Status = status;
    public void SetNote(string? note) => Note = note;
}
