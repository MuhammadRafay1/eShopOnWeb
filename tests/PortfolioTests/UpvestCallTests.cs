using System.Net;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.eShopWeb.Infrastructure.Portfolio.Services;
using Microsoft.Extensions.Logging.Abstractions;
using UpvestInvestmentApi.Core.Exceptions;
using Xunit;
using ReqUsers = UpvestInvestmentApi.Requests.Users;

namespace Microsoft.eShopWeb.PortfolioTests;

public class UpvestCallTests
{
    private static readonly NullLogger Logger = NullLogger.Instance;

    private static SdkConnectionException Unreachable() => new("unreachable", new HttpRequestException("reset"))
    {
        Method = HttpMethod.Post,
        RequestUri = new Uri("https://sandbox.upvest.co/users")
    };

    [Fact]
    public async Task Connection_failure_on_a_read_becomes_provider_unavailable()
    {
        var ex = await Assert.ThrowsAsync<PortfolioProviderException>(() =>
            UpvestCall.ReadAsync<int>(() => throw Unreachable(), "op", Logger));
        Assert.Null(ex.StatusCode);
    }

    [Fact]
    public async Task Connection_failure_on_a_write_becomes_unknown_outcome()
    {
        // A write that may have landed must never be reported as a plain failure.
        await Assert.ThrowsAsync<PortfolioUnknownOutcomeException>(() =>
            UpvestCall.WriteAsync<int>(() => throw Unreachable(), "op", Logger));
    }

    [Fact]
    public async Task Timeout_on_a_write_becomes_unknown_outcome()
    {
        var timeout = new SdkTimeoutException("no response within 15 s")
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://sandbox.upvest.co/users"),
            Timeout = TimeSpan.FromSeconds(15)
        };
        await Assert.ThrowsAsync<PortfolioUnknownOutcomeException>(() =>
            UpvestCall.WriteAsync<int>(() => throw timeout, "op", Logger));
    }

    [Fact]
    public async Task Api_error_status_is_carried_onto_the_domain_exception()
    {
        // A real SDK call over a stub that answers 400 -> ApiException -> PortfolioProviderException(400).
        var (client, _) = StubUpvest.BuildClient(_ => StubHandler.Json(HttpStatusCode.BadRequest, """{"detail":"bad"}"""));

        var ex = await Assert.ThrowsAsync<PortfolioProviderException>(() =>
            UpvestCall.ReadAsync(
                () => client.Users.RetrieveUser(new ReqUsers.RetrieveUserRequest { UserId = Guid.NewGuid() }),
                "RetrieveUser", Logger));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }
}
