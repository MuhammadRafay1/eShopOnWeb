using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PortfolioAggregate;

public class ModelPortfolioTests
{
    private static List<ModelFund> Funds(params (string Isin, decimal Weight)[] funds) =>
        funds.Select(f => new ModelFund(f.Isin, f.Weight)).ToList();

    [Fact]
    public void Constructs_when_weights_sum_to_100()
    {
        var model = new ModelPortfolio("balanced", "Balanced", Funds(("IE00B4L5Y983", 60m), ("IE00B5BMR087", 40m)));

        Assert.Equal("balanced", model.ModelId);
        Assert.Equal("Balanced", model.Name);
        Assert.Equal(2, model.Funds.Count);
        Assert.Null(model.UpvestAllocationId);
    }

    [Theory]
    [InlineData(60, 30)]   // sums to 90
    [InlineData(60, 50)]   // sums to 110
    public void Throws_when_weights_do_not_sum_to_100(int w1, int w2)
    {
        Assert.Throws<ArgumentException>(() =>
            new ModelPortfolio("m", "M", Funds(("IE00B4L5Y983", w1), ("IE00B5BMR087", w2))));
    }

    [Fact]
    public void Replace_swaps_funds_and_requires_sum_100()
    {
        var model = new ModelPortfolio("m", "M", Funds(("IE00B4L5Y983", 100m)));

        model.Replace("M2", Funds(("IE00B4L5Y983", 70m), ("IE00B5BMR087", 30m)));

        Assert.Equal("M2", model.Name);
        Assert.Equal(2, model.Funds.Count);
        Assert.Throws<ArgumentException>(() => model.Replace("bad", Funds(("IE00B4L5Y983", 10m))));
    }

    [Fact]
    public void LinkAllocation_records_the_upvest_allocation_id()
    {
        var model = new ModelPortfolio("m", "M", Funds(("IE00B4L5Y983", 100m)));
        var allocationId = Guid.NewGuid();

        model.LinkAllocation(allocationId);

        Assert.Equal(allocationId, model.UpvestAllocationId);
    }
}
