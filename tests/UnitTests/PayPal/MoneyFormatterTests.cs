using System.Globalization;
using Microsoft.eShopWeb.Infrastructure.PayPal;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.PayPal;

public class MoneyFormatterTests
{
    [Theory]
    [InlineData(29, "29.00")]
    [InlineData(8.5, "8.50")]
    [InlineData(1.239, "1.24")]
    public void ToWire_formats_two_decimals_invariant(decimal amount, string expected)
    {
        Assert.Equal(expected, MoneyFormatter.ToWire(amount));
    }

    [Fact]
    public void ToWire_uses_invariant_culture_even_under_comma_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // comma decimal separator
            Assert.Equal("1234.56", MoneyFormatter.ToWire(1234.56m));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ParseOrNull_parses_and_handles_invalid()
    {
        Assert.Equal(27.76m, MoneyFormatter.ParseOrNull("27.76"));
        Assert.Null(MoneyFormatter.ParseOrNull(null));
        Assert.Null(MoneyFormatter.ParseOrNull("not-a-number"));
    }
}
