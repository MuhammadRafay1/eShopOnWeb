using Microsoft.eShopWeb.ApplicationCore.Services;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.InvestingServiceTests;

public class RoundUpToNextEuro
{
    [Theory]
    [InlineData(12.30, 0.70)]
    [InlineData(8.50, 0.50)]
    [InlineData(0.01, 0.99)]
    [InlineData(99.99, 0.01)]
    public void SetsAsideTheGapToTheNextWholeEuro(decimal total, decimal expected)
    {
        Assert.Equal(expected, InvestingService.RoundUpToNextEuro(total));
    }

    [Theory]
    [InlineData(12.00)]
    [InlineData(17.00)]
    [InlineData(1.00)]
    public void SetsAsideNothingForAWholeEuroTotal(decimal total)
    {
        Assert.Equal(0m, InvestingService.RoundUpToNextEuro(total));
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(-5.00)]
    public void SetsAsideNothingForANonPositiveTotal(decimal total)
    {
        Assert.Equal(0m, InvestingService.RoundUpToNextEuro(total));
    }
}
