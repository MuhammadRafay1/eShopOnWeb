using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A realignment (rebalancing) run the operator triggered for a model. Its status is derived
/// from the live state of the Upvest rebalancing execution's orders.
/// </summary>
public class Realignment : BaseEntity, IAggregateRoot
{
    public Guid PublicId { get; private set; }
    public string ModelId { get; private set; }
    public string? UpvestExecutionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Realignment()
    {
        ModelId = string.Empty;
    }

    public Realignment(string modelId)
    {
        ModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        PublicId = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetExecutionId(string executionId)
    {
        UpvestExecutionId = Guard.Against.NullOrEmpty(executionId, nameof(executionId));
    }
}
