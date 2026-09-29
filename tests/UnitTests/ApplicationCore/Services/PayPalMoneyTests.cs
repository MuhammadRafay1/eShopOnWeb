using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services;

public class PayPalMoneyTests
{
    [Theory]
    [InlineData("USD", 10.5, "10.50")]
    [InlineData("USD", 10, "10.00")]
    [InlineData("EUR", 1234.567, "1234.57")]
    [InlineData("JPY", 1000, "1000")]     // 0-decimal currency
    [InlineData("JPY", 1000.4, "1000")]
    [InlineData("BHD", 1.2, "1.200")]     // 3-decimal currency
    [InlineData("KWD", 1.239, "1.239")]
    public void FormatsToCurrencyMinorUnits(string currency, decimal amount, string expected)
    {
        Assert.Equal(expected, PayPalMoney.Format(amount, currency));
    }

    [Theory]
    [InlineData("USD", 2)]
    [InlineData("JPY", 0)]
    [InlineData("BHD", 3)]
    [InlineData("XYZ", 2)] // unknown -> default 2
    public void DecimalPlacesByCurrency(string currency, int expected)
    {
        Assert.Equal(expected, PayPalMoney.DecimalPlaces(currency));
    }

    [Fact]
    public void ParsesInvariantDecimal()
    {
        Assert.Equal(35.06m, PayPalMoney.Parse("35.06"));
        Assert.Equal(0m, PayPalMoney.Parse(null));
    }
}
