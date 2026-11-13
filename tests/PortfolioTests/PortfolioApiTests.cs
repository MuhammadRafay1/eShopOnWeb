using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;
using Xunit;

namespace Microsoft.eShopWeb.PortfolioTests;

public class PortfolioApiTests
{
    [Theory]
    [InlineData(10, "10.00")]
    [InlineData(10.5, "10.50")]
    [InlineData(10.005, "10.01")]      // rounds away from zero to two places
    [InlineData(0, "0.00")]
    [InlineData(1234.5, "1234.50")]
    public void Money_always_has_two_decimal_places(decimal input, string expected)
    {
        // The wire scale is 2 regardless of the input scale -> JSON number renders with two places.
        Assert.Equal(expected, PortfolioApi.Money(input).ToString("0.00###", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Status_wire_values_are_lowercase()
    {
        Assert.Equal("pending", PortfolioApi.Wire(InvestorSignupStatus.Pending));
        Assert.Equal("active", PortfolioApi.Wire(InvestorSignupStatus.Active));
        Assert.Equal("rejected", PortfolioApi.Wire(InvestorSignupStatus.Rejected));
        Assert.Equal("invested", PortfolioApi.Wire(PaymentStatus.Invested));
        Assert.Equal("failed", PortfolioApi.Wire(PaymentStatus.Failed));
        Assert.Equal("completed", PortfolioApi.Wire(RealignmentStatus.Completed));
    }

    [Fact]
    public void Portfolio_response_rounds_money_to_two_places()
    {
        var view = new PortfolioView("balanced",
            new[] { new HoldingResult("IE00B4L5Y983", 3.5m, 12.3m) },
            12.3m, 10m);
        var r = PortfolioApi.ToResponse(view);
        Assert.Equal("balanced", r.ModelId);
        Assert.Equal(12.30m, r.TotalValue);
        Assert.Equal(10.00m, r.PaidIn);
        Assert.Equal(12.30m, r.Holdings[0].Value);
        Assert.Equal(3.5m, r.Holdings[0].Quantity);     // quantity is not money; not forced to 2dp
    }

    [Theory]
    [InlineData(typeof(PortfolioValidationException), 400)]
    [InlineData(typeof(PortfolioNotFoundException), 404)]
    [InlineData(typeof(PortfolioConflictException), 409)]
    public void Domain_exceptions_map_to_their_status(System.Type exType, int expected)
    {
        var ex = (PortfolioException)System.Activator.CreateInstance(exType, "msg")!;
        var result = (ObjectResult)PortfolioApi.ToErrorResult(ex);
        Assert.Equal(expected, result.StatusCode);
    }

    [Theory]
    [InlineData(401, 502)]   // our credentials -> provider unavailable, never surfaced as the caller's 401
    [InlineData(403, 502)]
    [InlineData(429, 503)]
    [InlineData(400, 400)]   // caller-fixable -> passed through
    [InlineData(404, 404)]
    [InlineData(500, 502)]
    public void Provider_errors_map_our_fault_to_5xx_and_caller_fault_through(int providerStatus, int expected)
    {
        var ex = new PortfolioProviderException("x", (System.Net.HttpStatusCode)providerStatus);
        var result = (ObjectResult)PortfolioApi.ToErrorResult(ex);
        Assert.Equal(expected, result.StatusCode);
    }
}
