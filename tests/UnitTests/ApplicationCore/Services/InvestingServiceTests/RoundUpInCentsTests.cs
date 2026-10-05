using Microsoft.eShopWeb.ApplicationCore.Services;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.InvestingServiceTests;

public class RoundUpInCentsTests
{
    [Theory]
    [InlineData(12.30, 70)]   // the worked example
    [InlineData(12.00, 0)]    // a whole number of euros sets aside nothing
    [InlineData(0.01, 99)]
    [InlineData(10.99, 1)]
    [InlineData(5.55, 45)]
    [InlineData(0, 0)]
    [InlineData(-3.20, 0)]    // guard against nonsensical totals
    [InlineData(100.00, 0)]
    public void ComputesChangeToNextWholeEuro(decimal orderTotal, long expectedCents)
    {
        Assert.Equal(expectedCents, InvestingService.RoundUpInCents(orderTotal));
    }
}
