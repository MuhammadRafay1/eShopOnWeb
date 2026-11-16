using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PortfolioTests;

public class ModelPortfolioTests
{
    private static List<ModelFund> Funds(params (string isin, decimal weight)[] funds)
    {
        var list = new List<ModelFund>();
        foreach (var (isin, weight) in funds)
        {
            list.Add(new ModelFund(isin, weight));
        }
        return list;
    }

    [Fact]
    public void CreatesWhenWeightsSumTo100()
    {
        var model = new ModelPortfolio(7, "Balanced", Funds(("IE00B0M62Q58", 60m), ("US0378331005", 40m)));

        Assert.Equal(7, model.Id);
        Assert.Equal("Balanced", model.Name);
        Assert.Equal(2, model.Funds.Count);
    }

    [Theory]
    [InlineData(60, 30)]   // 90
    [InlineData(70, 40)]   // 110
    public void RejectsWhenWeightsDoNotSumTo100(int a, int b)
    {
        Assert.Throws<ArgumentException>(() =>
            new ModelPortfolio(1, "Bad", Funds(("IE00B0M62Q58", a), ("US0378331005", b))));
    }

    [Fact]
    public void RejectsDuplicateFunds()
    {
        Assert.Throws<ArgumentException>(() =>
            new ModelPortfolio(1, "Dup", Funds(("IE00B0M62Q58", 50m), ("IE00B0M62Q58", 50m))));
    }

    [Fact]
    public void RejectsEmptyFunds()
    {
        Assert.Throws<ArgumentException>(() => new ModelPortfolio(1, "Empty", Funds()));
    }

    [Fact]
    public void UpdateReplacesFundsAndRevalidates()
    {
        var model = new ModelPortfolio(1, "M", Funds(("IE00B0M62Q58", 100m)));

        model.Update("M2", Funds(("IE00B0M62Q58", 50m), ("US0378331005", 50m)));

        Assert.Equal("M2", model.Name);
        Assert.Equal(2, model.Funds.Count);
    }
}
