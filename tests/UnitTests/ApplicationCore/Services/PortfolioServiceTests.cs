using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services;

public class PortfolioServiceTests
{
    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IRepository<ModelPortfolio> _models = Substitute.For<IRepository<ModelPortfolio>>();
    private readonly IRepository<Payment> _payments = Substitute.For<IRepository<Payment>>();
    private readonly IRepository<Realignment> _realignments = Substitute.For<IRepository<Realignment>>();
    private readonly IUpvestPortfolioService _upvest = Substitute.For<IUpvestPortfolioService>();
    private readonly IAppLogger<PortfolioService> _logger = Substitute.For<IAppLogger<PortfolioService>>();

    private PortfolioService CreateService() =>
        new(_investors, _models, _payments, _realignments, _upvest, _logger);

    private static InvestorSignUpForm SampleForm() => new(
        "Ada", "Lovelace", "ada@example.com", new DateOnly(1990, 1, 1),
        "London", "GB", "GB",
        new AddressForm("1 Analytical Way", "EC1A1AA", "London", "GB"),
        "441234567890", "TAX123", "GB",
        new IdentityDocumentForm("PASSPORT", "P1234567", "GB", new DateOnly(2032, 1, 1)));

    [Fact]
    public async Task SignUp_CreatesInvestor_AndOnboardsUpvestUser()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByBuyerIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Investor?>(null));
        _investors.AddAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<Investor>()));
        var upvestUserId = Guid.NewGuid();
        _upvest.CreateUserAsync(Arg.Any<InvestorOnboardingData>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(upvestUserId));
        _upvest.GetInvestorStatusAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(InvestorStatus.Pending));

        var investor = await CreateService().SignUpInvestorAsync("ada@example.com", SampleForm(), CancellationToken.None);

        Assert.Equal(upvestUserId, investor.UpvestUserId);
        Assert.Equal("ada@example.com", investor.BuyerId);
        await _investors.Received(1).AddAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>());
        await _upvest.Received(1).CreateUserAsync(Arg.Any<InvestorOnboardingData>(), investor.UserIdempotencyKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignUp_WhenUpvestRejectsUser_ReleasesClaim_AndThrows()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByBuyerIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Investor?>(null));
        _investors.AddAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<Investor>()));
        _upvest.CreateUserAsync(Arg.Any<InvestorOnboardingData>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task<Guid>>(_ => throw new UpvestIntegrationException("rejected", System.Net.HttpStatusCode.BadRequest));

        await Assert.ThrowsAsync<UpvestIntegrationException>(
            () => CreateService().SignUpInvestorAsync("ada@example.com", SampleForm(), CancellationToken.None));

        await _investors.Received(1).DeleteAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertModel_WhenWeightsDoNotSumTo100_Throws()
    {
        var funds = new List<FundWeight> { new("DE0000000001", 60m), new("DE0000000002", 30m) };

        await Assert.ThrowsAsync<PortfolioValidationException>(
            () => CreateService().UpsertModelAsync("balanced", "Balanced", funds, CancellationToken.None));

        await _upvest.DidNotReceive().CreateModelAllocationAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<FundWeight>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertModel_WhenIsinMalformed_Throws()
    {
        var funds = new List<FundWeight> { new("NOT-AN-ISIN", 100m) };

        await Assert.ThrowsAsync<PortfolioValidationException>(
            () => CreateService().UpsertModelAsync("m1", "Model", funds, CancellationToken.None));
    }

    [Fact]
    public async Task ChooseModel_WhenModelMissing_Throws()
    {
        var investor = NewInvestor();
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByBuyerIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Investor?>(investor));
        _models.FirstOrDefaultAsync(Arg.Any<ModelPortfolioByModelIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ModelPortfolio?>(null));

        await Assert.ThrowsAsync<PortfolioNotFoundException>(
            () => CreateService().ChooseModelAsync("ada@example.com", "missing", CancellationToken.None));
    }

    [Fact]
    public async Task PayIn_WhenNotAnInvestor_Throws()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByBuyerIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Investor?>(null));

        await Assert.ThrowsAsync<PortfolioConflictException>(
            () => CreateService().PayInAsync("ada@example.com", 100m, CancellationToken.None));
    }

    [Fact]
    public async Task PayIn_WhenInvestorNotActive_IsRefused()
    {
        var investor = NewInvestor(); // pending, no account
        _investors.FirstOrDefaultAsync(Arg.Any<InvestorByBuyerIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Investor?>(investor));

        await Assert.ThrowsAsync<PortfolioConflictException>(
            () => CreateService().PayInAsync("ada@example.com", 100m, CancellationToken.None));

        await _upvest.DidNotReceive().CreateTopUpAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerRealignment_WhenModelHasNoAllocation_Throws()
    {
        var model = new ModelPortfolio("m1", "Model");
        _models.FirstOrDefaultAsync(Arg.Any<ModelPortfolioByModelIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ModelPortfolio?>(model));

        await Assert.ThrowsAsync<PortfolioConflictException>(
            () => CreateService().TriggerRealignmentAsync("m1", CancellationToken.None));
    }

    private static Investor NewInvestor() => new(
        "ada@example.com", "Ada", "Lovelace", "ada@example.com", new DateOnly(1990, 1, 1),
        "London", "GB", "GB", "1 Analytical Way", "EC1A1AA", "London", "GB",
        "441234567890", "TAX123", "GB",
        "PASSPORT", "P1234567", "GB", new DateOnly(2032, 1, 1));
}
