using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.InvestingAggregate;

public class MoneyTests
{
    [Theory]
    [InlineData(12.30, 70)]   // €12.30 sets aside €0.70
    [InlineData(8.50, 50)]
    [InlineData(12.01, 99)]
    [InlineData(12.99, 1)]
    [InlineData(12.00, 0)]    // whole euro sets aside nothing
    [InlineData(0.00, 0)]
    [InlineData(19.50, 50)]
    public void RoundUpCents_sets_aside_difference_to_next_whole_euro(decimal total, long expectedCents)
    {
        Assert.Equal(expectedCents, Money.RoundUpCents(total));
    }

    [Theory]
    [InlineData(1000, "10.00")]
    [InlineData(70, "0.70")]
    [InlineData(5, "0.05")]
    [InlineData(123456, "1234.56")]
    public void CentsToAmountString_formats_two_decimals(long cents, string expected)
    {
        Assert.Equal(expected, Money.CentsToAmountString(cents));
    }

    [Theory]
    [InlineData(12.30, 1230)]
    [InlineData(10.00, 1000)]
    public void ToCents_roundtrips(decimal euros, long expectedCents)
    {
        Assert.Equal(expectedCents, Money.ToCents(euros));
        Assert.Equal(euros, Money.ToEuros(expectedCents));
    }
}
