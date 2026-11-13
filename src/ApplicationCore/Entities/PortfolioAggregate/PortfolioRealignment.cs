using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.ManagedPortfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment of every portfolio that follows a model. <see cref="RealignmentId"/> is
/// this application's own reference (the primary key and the id returned to the operator); the Upvest
/// rebalancing execution id is stored alongside so the status can be re-derived on GET.
/// </summary>
public class PortfolioRealignment : IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private PortfolioRealignment() { }
#pragma warning restore CS8618

    public PortfolioRealignment(Guid realignmentId, string modelId)
    {
        RealignmentId = Guard.Against.Default(realignmentId, nameof(realignmentId));
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        CreatedAt = DateTimeOffset.UtcNow;
        Status = RealignmentStatus.Pending;
    }

    public Guid RealignmentId { get; private set; }
    public string ModelId { get; private set; }
    public Guid? UpvestExecutionId { get; private set; }
    public RealignmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void RecordExecution(Guid executionId) => UpvestExecutionId = executionId;

    public void UpdateStatus(RealignmentStatus status) => Status = status;
}
