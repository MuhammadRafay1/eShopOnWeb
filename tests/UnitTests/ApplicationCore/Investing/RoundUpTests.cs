using Microsoft.eShopWeb.ApplicationCore.Investing;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Investing;

public class RoundUpTests
{
    [Theory]
    [InlineData(12.30, 0.70)]
    [InlineData(8.50, 0.50)]
    [InlineData(19.99, 0.01)]
    [InlineData(0.01, 0.99)]
    public void SetsAsideDifferenceToNextWholeEuro(decimal total, decimal expected)
    {
        Assert.Equal(expected, InvestingService.RoundUpOf(total));
    }

    [Theory]
    [InlineData(12.00)]
    [InlineData(10.00)]
    [InlineData(1.00)]
    public void SetsAsideNothingForWholeEuroTotals(decimal total)
    {
        Assert.Equal(0m, InvestingService.RoundUpOf(total));
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(-5.00)]
    public void SetsAsideNothingForNonPositiveTotals(decimal total)
    {
        Assert.Equal(0m, InvestingService.RoundUpOf(total));
    }
}
