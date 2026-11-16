using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment of every portfolio following a model. The shop generates
/// the <see cref="RealignmentId"/>; its outcome is read back from Upvest via <see cref="UpvestExecutionId"/>.
/// </summary>
public class Realignment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Realignment() { }
#pragma warning restore CS8618

    public Realignment(Guid realignmentId, string modelId)
    {
        RealignmentId = Guard.Against.Default(realignmentId, nameof(realignmentId));
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        CreatedAt = DateTimeOffset.UtcNow;
        LocalOutcome = RealignmentLocalOutcome.Pending;
    }

    /// <summary>The shop-generated realignment id returned to the caller. Unique.</summary>
    public Guid RealignmentId { get; private set; }

    /// <summary>The model whose followers are realigned.</summary>
    public string ModelId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The Upvest rebalancing-execution id once triggered.</summary>
    public Guid? UpvestExecutionId { get; private set; }

    /// <summary>Local outcome used only until an execution exists at Upvest.</summary>
    public RealignmentLocalOutcome LocalOutcome { get; private set; }

    public void SetExecution(Guid executionId)
    {
        UpvestExecutionId = executionId;
        LocalOutcome = RealignmentLocalOutcome.Pending;
    }

    public void MarkFailed() => LocalOutcome = RealignmentLocalOutcome.Failed;
}

/// <summary>The realignment outcome the shop knows before an Upvest execution exists.</summary>
public enum RealignmentLocalOutcome
{
    Pending = 0,
    Failed = 1
}
