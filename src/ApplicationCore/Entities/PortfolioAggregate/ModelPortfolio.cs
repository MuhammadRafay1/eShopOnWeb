using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An operator-defined model portfolio. <see cref="ModelId"/> is the operator's own identifier (the route
/// <c>{modelId}</c>); <see cref="AllocationId"/> is the Upvest portfolio-allocation UUID this model is
/// realized as, once the allocation has been created/replaced at Upvest.
/// </summary>
public class ModelPortfolio
{
    private ModelPortfolio()
    {
        ModelId = string.Empty;
        Name = string.Empty;
        _funds = new List<ModelPortfolioFund>();
    }

    public ModelPortfolio(string modelId, string name, IEnumerable<ModelPortfolioFund> funds)
    {
        ModelId = modelId;
        Name = name;
        _funds = funds.ToList();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string ModelId { get; private set; }
    public string Name { get; private set; }
    public Guid? AllocationId { get; set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<ModelPortfolioFund> _funds;
    public IReadOnlyCollection<ModelPortfolioFund> Funds => _funds.AsReadOnly();

    public void Replace(string name, IEnumerable<ModelPortfolioFund> funds)
    {
        Name = name;
        _funds.Clear();
        _funds.AddRange(funds);
    }
}

/// <summary>A fund within a model: its ISIN and target weight (a percentage; a model's weights sum to 100).</summary>
public class ModelPortfolioFund
{
    public ModelPortfolioFund(string instrumentId, decimal weight)
    {
        InstrumentId = instrumentId;
        Weight = weight;
    }

    public string InstrumentId { get; private set; }
    public decimal Weight { get; private set; }
}
