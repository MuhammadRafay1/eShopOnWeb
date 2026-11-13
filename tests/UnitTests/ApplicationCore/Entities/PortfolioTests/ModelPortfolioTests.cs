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
        var model = new ModelPortfolio("balanced", "Balanced", Funds(("IE00B4L5Y983", 60m), ("IE00B3RBWM25", 40m)));

        Assert.Equal("balanced", model.ModelId);
        Assert.Equal(2, model.Funds.Count);
        Assert.Equal(100m, model.Funds.Sum(f => f.Weight));
    }

    [Theory]
    [InlineData(60, 30)]   // sums to 90
    [InlineData(60, 50)]   // sums to 110
    public void RejectsWeightsThatDoNotSumTo100(int w1, int w2)
    {
        Assert.Throws<ArgumentException>(() =>
            new ModelPortfolio("m", "M", Funds(("IE00B4L5Y983", w1), ("IE00B3RBWM25", w2))));
    }

    [Fact]
    public void RejectsDuplicateFunds()
    {
        Assert.Throws<ArgumentException>(() =>
            new ModelPortfolio("m", "M", Funds(("IE00B4L5Y983", 50m), ("IE00B4L5Y983", 50m))));
    }

    [Fact]
    public void ReplaceUpdatesFundsAndName()
    {
        var model = new ModelPortfolio("m", "Old", Funds(("IE00B4L5Y983", 100m)));

        model.Replace("New", Funds(("IE00B4L5Y983", 70m), ("IE00B3RBWM25", 30m)));

        Assert.Equal("New", model.Name);
        Assert.Equal(2, model.Funds.Count);
    }

    [Fact]
    public void CapturesAllocationId()
    {
        var model = new ModelPortfolio("m", "M", Funds(("IE00B4L5Y983", 100m)));
        model.SetAllocationId("alloc-123");
        Assert.Equal("alloc-123", model.UpvestAllocationId);
    }
}
