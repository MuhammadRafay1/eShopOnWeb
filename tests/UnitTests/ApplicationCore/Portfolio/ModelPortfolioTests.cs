using System;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Portfolio;

public class ModelPortfolioTests
{
    [Fact]
    public void CreatesWhenWeightsSumTo100()
    {
        var model = new ModelPortfolio("m1", "Balanced", new[]
        {
            new ModelFund("IE00B4L5Y983", 60m),
            new ModelFund("IE00B3RBWM25", 40m),
        });

        Assert.Equal("m1", model.ModelId);
        Assert.Equal(2, model.Funds.Count);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(110)]
    public void RejectsWhenWeightsDoNotSumTo100(int firstWeight)
    {
        Assert.Throws<ArgumentException>(() => new ModelPortfolio("m1", "Bad", new[]
        {
            new ModelFund("IE00B4L5Y983", firstWeight),
        }));
    }

    [Fact]
    public void UpdateReplacesFunds()
    {
        var model = new ModelPortfolio("m1", "Balanced", new[] { new ModelFund("A", 100m) });
        model.Update("Balanced v2", new[] { new ModelFund("B", 30m), new ModelFund("C", 70m) });

        Assert.Equal("Balanced v2", model.Name);
        Assert.Equal(new[] { "B", "C" }, model.Funds.Select(f => f.InstrumentId));
    }

    [Fact]
    public void LinkAllocationStoresId()
    {
        var model = new ModelPortfolio("m1", "Balanced", new[] { new ModelFund("A", 100m) });
        var allocationId = Guid.NewGuid();
        model.LinkAllocation(allocationId);
        Assert.Equal(allocationId, model.UpvestAllocationId);
    }
}
