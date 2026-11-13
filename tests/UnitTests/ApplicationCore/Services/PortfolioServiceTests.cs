using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services;

public class PortfolioServiceTests
{
    private const string BuyerId = "shopper@example.com";

    private readonly IRepository<PortfolioInvestor> _investors = Substitute.For<IRepository<PortfolioInvestor>>();
    private readonly IRepository<ModelPortfolio> _models = Substitute.For<IRepository<ModelPortfolio>>();
    private readonly IRepository<PortfolioPayment> _payments = Substitute.For<IRepository<PortfolioPayment>>();
    private readonly IRepository<PortfolioRealignment> _realignments = Substitute.For<IRepository<PortfolioRealignment>>();
    private readonly IUpvestPortfolioGateway _upvest = Substitute.For<IUpvestPortfolioGateway>();
    private readonly IAppLogger<PortfolioService> _logger = Substitute.For<IAppLogger<PortfolioService>>();

    private PortfolioService CreateService() =>
        new(_investors, _models, _payments, _realignments, _upvest, _logger);

    private static List<ModelFund> ValidFunds() => new()
    {
        new ModelFund("IE00B4L5Y983", 60m),
        new ModelFund("IE00B3RBWM25", 40m),
    };

    [Fact]
    public async Task UpsertModel_WeightsNotHundred_ThrowsValidation()
    {
        var service = CreateService();
        var funds = new List<ModelFund> { new("IE00B4L5Y983", 60m), new("IE00B3RBWM25", 30m) };

        await Assert.ThrowsAsync<PortfolioValidationException>(
            () => service.UpsertModelAsync("balanced", "Balanced", funds, CancellationToken.None));
    }

    [Fact]
    public async Task UpsertModel_DuplicateIsin_ThrowsValidation()
    {
        var service = CreateService();
        var funds = new List<ModelFund> { new("IE00B4L5Y983", 50m), new("IE00B4L5Y983", 50m) };

        await Assert.ThrowsAsync<PortfolioValidationException>(
            () => service.UpsertModelAsync("balanced", "Balanced", funds, CancellationToken.None));
    }

    [Fact]
    public async Task UpsertModel_Valid_CreatesModelAndReturnsFunds()
    {
        var service = CreateService();
        _models.GetByIdAsync("balanced", Arg.Any<CancellationToken>()).Returns((ModelPortfolio?)null);
        _upvest.CreateModelAllocationAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ModelFund>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var view = await service.UpsertModelAsync("balanced", "Balanced", ValidFunds(), CancellationToken.None);

        Assert.Equal("balanced", view.ModelId);
        Assert.Equal("Balanced", view.Name);
        Assert.Equal(2, view.Funds.Count);
        await _models.Received(1).AddAsync(Arg.Any<ModelPortfolio>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertModel_WhenInstrumentNotOffered_ThrowsValidation()
    {
        var service = CreateService();
        _upvest.EnsureInstrumentsExistAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PortfolioValidationException("'X' is not an instrument Upvest offers.")));

        await Assert.ThrowsAsync<PortfolioValidationException>(
            () => service.UpsertModelAsync("balanced", "Balanced", ValidFunds(), CancellationToken.None));
    }

    [Fact]
    public async Task GetInvestor_WhenNoneSignedUp_ThrowsNotFound()
    {
        var service = CreateService();
        _investors.GetByIdAsync(BuyerId, Arg.Any<CancellationToken>()).Returns((PortfolioInvestor?)null);

        await Assert.ThrowsAsync<PortfolioNotFoundException>(
            () => service.GetInvestorAsync(BuyerId, CancellationToken.None));
    }

    [Fact]
    public async Task SignUp_WhenUpvestRejectsData_MarksRejected()
    {
        var service = CreateService();
        _investors.GetByIdAsync(BuyerId, Arg.Any<CancellationToken>()).Returns((PortfolioInvestor?)null);
        _upvest.CreateUserAsync(Arg.Any<InvestorRegistration>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Guid>(_ => throw new UpvestGatewayException("rejected", 400, outcomeUnknown: false));

        var view = await service.SignUpInvestorAsync(BuyerId, ValidRegistration(), CancellationToken.None);

        Assert.Equal("rejected", view.Status);
    }

    [Fact]
    public async Task SignUp_WhenScopeRefused_StaysPending()
    {
        var service = CreateService();
        _investors.GetByIdAsync(BuyerId, Arg.Any<CancellationToken>()).Returns((PortfolioInvestor?)null);
        // User created, but the next step is refused for lack of scope (401) -> pending, not rejected.
        _upvest.CreateUserAsync(Arg.Any<InvestorRegistration>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());
        _upvest.CreateAccountGroupAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Guid>(_ => throw new UpvestGatewayException("no scope", 401, outcomeUnknown: false));

        var view = await service.SignUpInvestorAsync(BuyerId, ValidRegistration(), CancellationToken.None);

        Assert.Equal("pending", view.Status);
    }

    [Fact]
    public async Task PayIn_WhenInvestorNotActive_ThrowsConflict()
    {
        var service = CreateService();
        _investors.GetByIdAsync(BuyerId, Arg.Any<CancellationToken>()).Returns(new PortfolioInvestor(BuyerId));

        await Assert.ThrowsAsync<PortfolioConflictException>(
            () => service.PayInAsync(BuyerId, 100m, CancellationToken.None));
    }

    [Fact]
    public async Task ChooseModel_UnknownModel_ThrowsNotFound()
    {
        var service = CreateService();
        _investors.GetByIdAsync(BuyerId, Arg.Any<CancellationToken>()).Returns(new PortfolioInvestor(BuyerId));
        _models.GetByIdAsync("nope", Arg.Any<CancellationToken>()).Returns((ModelPortfolio?)null);

        await Assert.ThrowsAsync<PortfolioNotFoundException>(
            () => service.ChooseModelAsync(BuyerId, "nope", CancellationToken.None));
    }

    private static InvestorRegistration ValidRegistration() => new(
        FirstName: "Max",
        LastName: "Mustermann",
        Email: "max@example.de",
        BirthDate: "1988-04-12",
        BirthCity: "Berlin",
        BirthCountry: "DE",
        Nationality: "DE",
        Address: new AddressInfo("Alexanderplatz 1", "10178", "Berlin", "DE"),
        PhoneNumber: "491701234567",
        TaxId: "12345678901",
        TaxCountry: "DE",
        IdentityDocument: new IdentityDocument("ID_CARD", "T22000129", "DE", "2031-03-15"));
}
