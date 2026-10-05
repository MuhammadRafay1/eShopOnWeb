using Microsoft.eShopWeb.ApplicationCore.Services;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services;

public class SpareChangeCalculatorTests
{
    [Theory]
    [InlineData(12.30, 0.70)]   // sets aside the difference to the next euro
    [InlineData(15.00, 0.00)]   // a whole number of euros sets aside nothing
    [InlineData(12.01, 0.99)]
    [InlineData(0.01, 0.99)]
    [InlineData(8.50, 0.50)]
    [InlineData(0.00, 0.00)]    // nothing ordered, nothing set aside
    public void RoundsUpToTheNextEuro(decimal orderTotal, decimal expected)
    {
        Assert.Equal(expected, SpareChangeCalculator.RoundUpToNextEuro(orderTotal));
    }
}
