using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PortfolioAggregate;

public class ModelPortfolioTests
{
    private static List<ModelFund> Funds(params (string isin, decimal weight)[] funds)
    {
        var list = new List<ModelFund>();
        foreach (var (isin, weight) in funds)
            list.Add(new ModelFund(isin, weight));
        return list;
    }

    [Fact]
    public void CreatesWhenWeightsSumToExactlyOneHundred()
    {
        var model = new ModelPortfolio("balanced", "Balanced",
            Funds(("IE00B4L5Y983", 60m), ("IE00B3RBWM25", 40m)));

        Assert.Equal("balanced", model.ModelId);
        Assert.Equal(2, model.Funds.Count);
    }

    [Theory]
    [InlineData(60, 30)]   // sums to 90
    [InlineData(60, 50)]   // sums to 110
    public void RejectsWhenWeightsDoNotSumToOneHundred(int w1, int w2)
    {
        Assert.Throws<ArgumentException>(() => new ModelPortfolio("m", "M",
            Funds(("A", w1), ("B", w2))));
    }

    [Fact]
    public void ReplacingDefinitionUpdatesNameAndFunds()
    {
        var model = new ModelPortfolio("m", "First", Funds(("A", 100m)));

        model.SetDefinition("Second", Funds(("B", 70m), ("C", 30m)));

        Assert.Equal("Second", model.Name);
        Assert.Equal(2, model.Funds.Count);
    }

    [Fact]
    public void RejectsEmptyFundList()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ModelPortfolio("m", "M", new List<ModelFund>()));
    }
}
