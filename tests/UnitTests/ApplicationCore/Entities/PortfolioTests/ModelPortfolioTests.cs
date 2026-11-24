using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PortfolioTests;

public class ModelPortfolioTests
{
    private static List<ModelFund> Funds(params (string isin, decimal weight)[] funds) =>
        funds.Select(f => new ModelFund(f.isin, f.weight)).ToList();

    [Fact]
    public void CreatesWhenWeightsSumTo100()
    {
        var model = new ModelPortfolio("balanced", "Balanced",
            Funds(("IE00B4L5Y983", 60m), ("IE00B3RBWM25", 40m)));

        Assert.Equal("balanced", model.ModelId);
        Assert.Equal(2, model.Funds.Count);
    }

    [Theory]
    [InlineData(60, 30)]   // sums to 90
    [InlineData(70, 40)]   // sums to 110
    public void ThrowsWhenWeightsDoNotSumTo100(int w1, int w2)
    {
        Assert.Throws<ArgumentException>(() =>
            new ModelPortfolio("m", "M", Funds(("A", w1), ("B", w2))));
    }

    [Fact]
    public void ThrowsWhenNoFunds()
    {
        Assert.Throws<ArgumentException>(() =>
            new ModelPortfolio("m", "M", new List<ModelFund>()));
    }

    [Fact]
    public void ReplaceSwapsFundsAndKeepsAllocationId()
    {
        var model = new ModelPortfolio("m", "M", Funds(("A", 100m)));
        model.SetAllocationId("alloc-123");

        model.Replace("M2", Funds(("B", 50m), ("C", 50m)));

        Assert.Equal("M2", model.Name);
        Assert.Equal("alloc-123", model.UpvestAllocationId);
        Assert.Equal(2, model.Funds.Count);
        Assert.Contains(model.Funds, f => f.InstrumentId == "B");
    }

    [Fact]
    public void FundWeightOutOfRangeThrows()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ModelFund("A", 150m));
    }
}
