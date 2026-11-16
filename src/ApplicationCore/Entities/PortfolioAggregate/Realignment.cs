using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-triggered realignment (Upvest rebalancing execution). <see cref="Id"/> is the Upvest
/// execution id (also the public realignmentId); status is always read back from Upvest.
/// </summary>
public class Realignment
{
    private Realignment() { ModelId = string.Empty; }

    public Realignment(Guid id, string modelId, Guid idempotencyKey)
    {
        Id = id;
        ModelId = modelId;
        IdempotencyKey = idempotencyKey;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string ModelId { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
