using Microsoft.eShopWeb.Infrastructure.Payments.PayPal;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.PayPal;

public class PayPalOptionsTests
{
    [Fact]
    public void DefaultsToSandbox()
    {
        var options = new PayPalOptions { Environment = "sandbox" };
        Assert.Equal("https://api-m.sandbox.paypal.com", options.ResolveBaseUrl());
    }

    [Theory]
    [InlineData("live")]
    [InlineData("production")]
    [InlineData("LIVE")]
    public void LiveMapsToLiveHost(string env)
    {
        var options = new PayPalOptions { Environment = env };
        Assert.Equal("https://api-m.paypal.com", options.ResolveBaseUrl());
    }

    [Fact]
    public void ExplicitBaseUrlOverridesEnvironmentAndIsTrimmed()
    {
        var options = new PayPalOptions
        {
            Environment = "live",
            BaseUrl = "https://mock.paypal.local/api/"
        };
        Assert.Equal("https://mock.paypal.local/api", options.ResolveBaseUrl());
    }
}
