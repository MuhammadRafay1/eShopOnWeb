using Microsoft.eShopWeb.ApplicationCore.Services.Payments;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.Payments;

public class CurrencyFormatTests
{
    [Theory]
    [InlineData(19.5, "USD", "19.50")]
    [InlineData(19.5, "EUR", "19.50")]
    [InlineData(100, "JPY", "100")]
    [InlineData(1.234, "BHD", "1.234")]
    public void Format_UsesCorrectDecimalsPerCurrency(decimal amount, string currency, string expected)
    {
        Assert.Equal(expected, CurrencyFormat.Format(amount, currency));
    }

    [Fact]
    public void Parse_RoundTripsFormattedValue()
    {
        var formatted = CurrencyFormat.Format(42.37m, "USD");
        Assert.Equal(42.37m, CurrencyFormat.Parse(formatted));
    }
}
