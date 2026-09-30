using Microsoft.eShopWeb;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore;

public class PayPalSettingsTests
{
    [Fact]
    public void ResolvedBaseUrl_ExplicitOverride_UsedVerbatimEvenInLiveEnvironment()
    {
        var settings = new PayPalSettings { Environment = "live", BaseUrl = "https://example-override.test/" };

        Assert.Equal("https://example-override.test", settings.ResolvedBaseUrl());
    }

    [Theory]
    [InlineData("sandbox", "https://api-m.sandbox.paypal.com")]
    [InlineData("live", "https://api-m.paypal.com")]
    [InlineData("production", "https://api-m.paypal.com")]
    [InlineData("LIVE", "https://api-m.paypal.com")]
    public void ResolvedBaseUrl_DerivesFromEnvironment_WhenNoOverride(string environment, string expected)
    {
        var settings = new PayPalSettings { Environment = environment, BaseUrl = null };

        Assert.Equal(expected, settings.ResolvedBaseUrl());
    }
}
