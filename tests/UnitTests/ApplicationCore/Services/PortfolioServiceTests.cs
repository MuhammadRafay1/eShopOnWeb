using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.ApplicationCore.Upvest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services;

public class PortfolioServiceTests
{
    private readonly IUpvestClient _upvest = Substitute.For<IUpvestClient>();
    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IRepository<Payment> _payments = Substitute.For<IRepository<Payment>>();
    private readonly IRepository<ModelPortfolio> _models = Substitute.For<IRepository<ModelPortfolio>>();
    private readonly IRepository<Realignment> _realignments = Substitute.For<IRepository<Realignment>>();
    private readonly IAppLogger<PortfolioService> _logger = Substitute.For<IAppLogger<PortfolioService>>();

    private PortfolioService CreateService() =>
        new(_upvest, _investors, _payments, _models, _realignments, _logger);

    private static InvestorSignUp SampleForm() => new(
        "Ada", "Lovelace", "ada@example.com",
        new System.DateOnly(1985, 3, 14), "Berlin", "DE", "DE",
        new SignUpAddress("Alexanderplatz 1", "10178", "Berlin", "DE"),
        "+49 151 23456789", "DE123", "DE",
        new SignUpIdentityDocument("PASSPORT", "C01X00T47", "DE", new System.DateOnly(2030, 1, 1)));

    [Fact]
    public async Task SignUp_CreatesUpvestUser_ReturnsPendingWithUpvestId()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperSpecification>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);
        _upvest.CreateUserAsync(Arg.Any<UpvestCreateUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestUser("uid-123", "INACTIVE"));
        // Non-user operations are not authorised in this environment; the flow degrades gracefully.
        _upvest.CreateKycCheckAsync(Arg.Any<string>(), Arg.Any<UpvestKycCheckRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UpvestAuthorizationException(401, null, null, "not authorised"));
        _upvest.CreateAccountGroupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UpvestAuthorizationException(401, null, null, "not authorised"));
        _upvest.GetUserAsync("uid-123", Arg.Any<CancellationToken>())
            .Returns(new UpvestUser("uid-123", "INACTIVE"));

        var view = await CreateService().SignUpInvestorAsync("shopper-1", SampleForm());

        Assert.Equal("uid-123", view.InvestorId);
        Assert.Equal("pending", view.Status);
        await _upvest.Received(1).CreateUserAsync(Arg.Any<UpvestCreateUserRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PayIn_WhenNotAnInvestor_IsRefused()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperSpecification>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);

        await Assert.ThrowsAsync<PortfolioValidationException>(
            () => CreateService().PayInAsync("shopper-1", 100m));
    }

    [Fact]
    public async Task PayIn_WhenInvestorNotActive_IsRefused()
    {
        var investor = new Investor("shopper-1");
        investor.SetUpvestUser("uid-123");
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperSpecification>(), Arg.Any<CancellationToken>())
            .Returns(investor);
        _upvest.GetUserAsync("uid-123", Arg.Any<CancellationToken>())
            .Returns(new UpvestUser("uid-123", "INACTIVE"));

        await Assert.ThrowsAsync<PortfolioValidationException>(
            () => CreateService().PayInAsync("shopper-1", 100m));
    }

    [Fact]
    public async Task PayIn_WithNonPositiveAmount_IsRefused()
    {
        await Assert.ThrowsAsync<PortfolioValidationException>(
            () => CreateService().PayInAsync("shopper-1", 0m));
    }

    [Fact]
    public async Task ChooseModel_WhenModelMissing_ThrowsNotFound()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByShopperSpecification>(), Arg.Any<CancellationToken>())
            .Returns(new Investor("shopper-1"));
        _models.FirstOrDefaultAsync(Arg.Any<ModelPortfolioByModelIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns((ModelPortfolio?)null);

        await Assert.ThrowsAsync<PortfolioNotFoundException>(
            () => CreateService().ChooseModelAsync("shopper-1", "nope"));
    }
}
