using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Investing;

public class ChangeMathTests
{
    [Theory]
    [InlineData(12.30, 70)]   // €12.30 sets aside €0.70
    [InlineData(8.50, 50)]
    [InlineData(13.00, 0)]    // whole euro sets aside nothing
    [InlineData(0.01, 99)]
    [InlineData(99.99, 1)]
    [InlineData(0.00, 0)]
    public void RoundUpCents_sets_aside_the_difference_to_the_next_whole_euro(decimal total, long expectedCents)
    {
        Assert.Equal(expectedCents, ChangeMath.RoundUpCents(total));
    }
}
