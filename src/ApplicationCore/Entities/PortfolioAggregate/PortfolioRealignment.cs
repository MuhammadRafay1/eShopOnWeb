using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment that brings every portfolio following a model back to the model's
/// current weights. Tracks the Upvest rebalancing execution it maps to and its outcome.
/// </summary>
public class PortfolioRealignment : BaseEntity, IAggregateRoot
{
    /// <summary>The caller-facing realignment identifier, chosen by the app before the Upvest call.</summary>
    public Guid RealignmentKey { get; private set; }

    /// <summary>The app-side model (<see cref="ModelPortfolio.ModelKey"/>) this realignment targets.</summary>
    public string ModelKey { get; private set; } = default!;

    public Guid? UpvestExecutionId { get; private set; }

    public RealignmentStatus Status { get; private set; } = RealignmentStatus.Pending;

    public Guid IdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private PortfolioRealignment() { } // EF

    public PortfolioRealignment(string modelKey)
    {
        RealignmentKey = Guid.NewGuid();
        ModelKey = modelKey;
        Status = RealignmentStatus.Pending;
        IdempotencyKey = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetExecution(Guid executionId) => UpvestExecutionId = executionId;
    public void SetStatus(RealignmentStatus status) => Status = status;
}
