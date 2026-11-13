using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PortfolioTests;

public class ModelPortfolioTests
{
    private static ModelFund Fund(string isin, decimal weight) => new(isin, weight);

    [Fact]
    public void CreatesWhenWeightsSumTo100()
    {
        var model = new ModelPortfolio("balanced", "Balanced", new[]
        {
            Fund("IE00B4L5Y983", 60m),
            Fund("IE00B3RBWM25", 40m)
        });

        Assert.Equal("balanced", model.ModelId);
        Assert.Equal(2, model.Funds.Count);
        Assert.Null(model.UpvestAllocationId);
    }

    [Fact]
    public void ThrowsWhenWeightsDoNotSumTo100()
    {
        Assert.Throws<ArgumentException>(() => new ModelPortfolio("bad", "Bad", new[]
        {
            Fund("IE00B4L5Y983", 60m),
            Fund("IE00B3RBWM25", 30m)
        }));
    }

    [Fact]
    public void ThrowsWhenNoFunds()
    {
        Assert.Throws<ArgumentException>(() => new ModelPortfolio("empty", "Empty", new List<ModelFund>()));
    }

    [Fact]
    public void ReplaceUpdatesFundsAndValidatesWeights()
    {
        var model = new ModelPortfolio("m", "M", new[] { Fund("IE00B4L5Y983", 100m) });

        model.Replace("M2", new[] { Fund("IE00B4L5Y983", 50m), Fund("IE00B3RBWM25", 50m) });

        Assert.Equal("M2", model.Name);
        Assert.Equal(2, model.Funds.Count);
        Assert.Throws<ArgumentException>(() => model.Replace("M3", new[] { Fund("IE00B4L5Y983", 99m) }));
    }

    [Fact]
    public void SetUpvestAllocationRecordsTheMapping()
    {
        var model = new ModelPortfolio("m", "M", new[] { Fund("IE00B4L5Y983", 100m) });
        var id = Guid.NewGuid();

        model.SetUpvestAllocation(id);

        Assert.Equal(id, model.UpvestAllocationId);
    }
}
