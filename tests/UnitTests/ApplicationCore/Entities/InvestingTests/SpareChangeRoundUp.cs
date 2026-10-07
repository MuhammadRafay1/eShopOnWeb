using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.InvestingTests;

public class SpareChangeRoundUp
{
    [Theory]
    [InlineData(12.30, 0.70)]
    [InlineData(19.50, 0.50)]
    [InlineData(0.01, 0.99)]
    [InlineData(99.99, 0.01)]
    public void RoundsUpToTheNextWholeEuro(decimal total, decimal expected)
    {
        Assert.Equal(expected, SpareChange.RoundUp(total));
    }

    [Theory]
    [InlineData(12.00)]
    [InlineData(1.00)]
    [InlineData(0.00)]
    public void SetsAsideNothingForAWholeEuroTotal(decimal total)
    {
        Assert.Equal(0m, SpareChange.RoundUp(total));
    }
}
