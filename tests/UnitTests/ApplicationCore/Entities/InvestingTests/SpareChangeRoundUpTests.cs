using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.InvestingTests;

public class SpareChangeRoundUpTests
{
    [Theory]
    [InlineData(12.30, 0.70)]   // sets aside the difference to the next whole euro
    [InlineData(8.50, 0.50)]
    [InlineData(0.01, 0.99)]
    [InlineData(12.00, 0.00)]   // already a whole number of euros -> nothing
    [InlineData(100.00, 0.00)]
    [InlineData(0.00, 0.00)]
    public void RoundsUpToNextWholeEuro(decimal total, decimal expected)
    {
        Assert.Equal(expected, SpareChange.RoundUp(total));
    }
}
