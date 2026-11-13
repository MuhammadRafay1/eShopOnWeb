using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Domain;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight per fund. The operator owns
/// the set of models; each maps to one Upvest portfolio allocation.
/// </summary>
public class ModelPortfolio
{
    /// <summary>Operator-chosen identifier for the model. Primary key.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Display name of the model.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Upvest allocation id backing this model, once created at Upvest.</summary>
    public Guid? AllocationId { get; set; }

    /// <summary>The funds and their target weights (percentages summing to exactly 100).</summary>
    public List<ModelFund> Funds { get; set; } = new();

    /// <summary>Persisted idempotency key for the allocation-create write (reused on retry).</summary>
    public Guid AllocationIdempotencyKey { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>One fund (by ISIN) in a model portfolio, with its target weight as a percentage.</summary>
public class ModelFund
{
    public int Id { get; set; }

    /// <summary>Owning model id (FK).</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>The fund's ISIN.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Target weight as a percentage (0–100).</summary>
    public decimal Weight { get; set; }
}
