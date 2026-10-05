using Microsoft.eShopWeb.ApplicationCore.Investing;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Investing;

public class MoneyTests
{
    [Theory]
    [InlineData(12.30, 70)]   // €12.30 -> set aside €0.70
    [InlineData(8.50, 50)]
    [InlineData(12.00, 0)]    // whole euro -> nothing
    [InlineData(0.01, 99)]
    [InlineData(0.00, 0)]
    [InlineData(100.99, 1)]
    public void RoundUpCents_rounds_up_to_the_next_whole_euro(decimal euros, long expectedCents)
    {
        Assert.Equal(expectedCents, Money.RoundUpCents(euros));
    }

    [Theory]
    [InlineData(12.30, 1230)]
    [InlineData(10.00, 1000)]
    public void ToCents_converts_euros_to_cents(decimal euros, long expectedCents)
    {
        Assert.Equal(expectedCents, Money.ToCents(euros));
    }
}
