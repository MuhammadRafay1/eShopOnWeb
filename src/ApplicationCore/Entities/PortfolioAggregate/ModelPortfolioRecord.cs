using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio: a named mix of funds with a target weight per fund.
/// The application owns the <see cref="ModelId"/> (the path value a caller uses) and maps it to the
/// Upvest portfolio allocation that backs it.
/// </summary>
public class ModelPortfolioRecord
{
    /// <summary>The operator-chosen model id (the <c>{modelId}</c> path value). Primary key.</summary>
    public string ModelId { get; set; } = default!;

    /// <summary>The Upvest portfolio-allocation id this model is backed by.</summary>
    public Guid AllocationId { get; set; }

    public string Name { get; set; } = default!;

    /// <summary>The funds and their target weights (weights sum to exactly 100).</summary>
    public List<ModelFund> Funds { get; set; } = new();

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// One fund in a model portfolio: an instrument (by ISIN) and its target weight as a percentage.
/// </summary>
public class ModelFund
{
    public int Id { get; set; }

    /// <summary>The fund's ISIN.</summary>
    public string InstrumentId { get; set; } = default!;

    /// <summary>The target weight as a percentage (model weights sum to exactly 100).</summary>
    public decimal Weight { get; set; }

    public string ModelId { get; set; } = default!;
}
