using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Upvest;
using Microsoft.eShopWeb.Infrastructure.Data;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Upvest;

public class ManagedPortfolioServiceTests
{
    private readonly CatalogContext _context;
    private readonly IUpvestInvestmentService _upvest = Substitute.For<IUpvestInvestmentService>();
    private readonly ManagedPortfolioService _service;

    public ManagedPortfolioServiceTests()
    {
        var options = new DbContextOptionsBuilder<CatalogContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new CatalogContext(options);

        _service = new ManagedPortfolioService(
            new EfRepository<Investor>(_context),
            new EfRepository<ModelPortfolio>(_context),
            new EfRepository<PortfolioPayment>(_context),
            new EfRepository<Realignment>(_context),
            _upvest,
            Substitute.For<IAppLogger<ManagedPortfolioService>>());
    }

    private static InvestorSignUp SampleSignUp() => new(
        "Alex", "Muster", "alex@example.com", new DateOnly(1990, 5, 14), "Berlin", "DE", "DE",
        new AddressInput("Street 1", "10115", "Berlin", "DE"), "+491700000000", "12345678901", "DE",
        new IdentityDocumentInput("PASSPORT", "C01X00T47", "DE", new DateOnly(2030, 1, 1)));

    private void ArrangeFullOnboarding()
    {
        _upvest.CreateUserAsync(Arg.Any<UpvestUserRegistration>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestUser("u1", "INACTIVE"));
        _upvest.CreateKycCheckAsync("u1", Arg.Any<UpvestKycCheck>(), Arg.Any<CancellationToken>())
            .Returns("chk1");
        _upvest.CreateAccountGroupAsync("u1", Arg.Any<CancellationToken>())
            .Returns(new UpvestAccountGroup("g1", "ACTIVE", "000000001"));
        _upvest.CreateAccountAsync("u1", "g1", Arg.Any<CancellationToken>())
            .Returns(new UpvestAccount("a1", "ACTIVE"));
        _upvest.GetUserAsync("u1", Arg.Any<CancellationToken>()).Returns(new UpvestUser("u1", "ACTIVE"));
        _upvest.GetAccountAsync("a1", Arg.Any<CancellationToken>()).Returns(new UpvestAccount("a1", "ACTIVE"));
    }

    [Fact]
    public async Task SignUp_creates_investor_and_links_upvest_user()
    {
        ArrangeFullOnboarding();

        var investor = await _service.SignUpInvestorAsync("shopper-1", SampleSignUp());

        Assert.Equal("u1", investor.UpvestUserId);
        Assert.Equal(InvestorStatus.Active, investor.Status);
        await _upvest.Received(1).CreateUserAsync(Arg.Any<UpvestUserRegistration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignUp_twice_for_same_shopper_is_rejected()
    {
        ArrangeFullOnboarding();
        await _service.SignUpInvestorAsync("shopper-1", SampleSignUp());

        await Assert.ThrowsAsync<DuplicateInvestorException>(
            () => _service.SignUpInvestorAsync("shopper-1", SampleSignUp()));
    }

    [Fact]
    public async Task One_shopper_never_sees_anothers_investor()
    {
        ArrangeFullOnboarding();
        await _service.SignUpInvestorAsync("shopper-1", SampleSignUp());

        var other = await _service.GetInvestorAsync("shopper-2");
        Assert.Null(other);
    }

    [Fact]
    public async Task UpsertModel_rejects_weights_that_do_not_sum_to_100()
    {
        var funds = new List<ModelFundInput> { new("IE00B4L5Y983", 50), new("IE00B3RBWM25", 40) };
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpsertModelAsync("m1", "Bad", funds));
    }

    [Fact]
    public async Task UpsertModel_persists_and_lists_the_model()
    {
        _upvest.CreateAllocationAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UpvestAllocationEntry>>(), Arg.Any<CancellationToken>())
            .Returns("alloc-1");
        var funds = new List<ModelFundInput> { new("IE00B4L5Y983", 60), new("IE00B3RBWM25", 40) };

        var model = await _service.UpsertModelAsync("balanced", "Balanced", funds);
        Assert.Equal("alloc-1", model.UpvestAllocationId);

        var all = await _service.ListModelsAsync();
        Assert.Single(all);
        Assert.Equal("balanced", all[0].ModelId);
        Assert.Equal(2, all[0].Funds.Count);
    }

    [Fact]
    public async Task ChooseModel_for_unknown_model_throws()
    {
        ArrangeFullOnboarding();
        await _service.SignUpInvestorAsync("shopper-1", SampleSignUp());

        await Assert.ThrowsAsync<ModelPortfolioNotFoundException>(
            () => _service.ChooseModelAsync("shopper-1", "nope"));
    }

    [Fact]
    public async Task PayIn_is_refused_when_investor_is_not_active()
    {
        // Onboarding where the account never becomes ready.
        _upvest.CreateUserAsync(Arg.Any<UpvestUserRegistration>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestUser("u1", "INACTIVE"));
        _upvest.CreateKycCheckAsync("u1", Arg.Any<UpvestKycCheck>(), Arg.Any<CancellationToken>()).Returns("chk1");
        _upvest.CreateAccountGroupAsync("u1", Arg.Any<CancellationToken>())
            .Returns(new UpvestAccountGroup("g1", "PENDING_APPROVAL", "000000001"));
        _upvest.CreateAccountAsync("u1", "g1", Arg.Any<CancellationToken>())
            .Returns(new UpvestAccount("a1", "PENDING_APPROVAL"));
        _upvest.GetUserAsync("u1", Arg.Any<CancellationToken>()).Returns(new UpvestUser("u1", "INACTIVE"));
        _upvest.GetAccountAsync("a1", Arg.Any<CancellationToken>()).Returns(new UpvestAccount("a1", "PENDING_APPROVAL"));

        await _service.SignUpInvestorAsync("shopper-1", SampleSignUp());

        await Assert.ThrowsAsync<InvestorNotReadyException>(() => _service.PayInAsync("shopper-1", 100m));
    }

    [Fact]
    public async Task PayIn_credits_and_invests_and_reports_invested()
    {
        ArrangeFullOnboarding();
        _upvest.CreateAllocationAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UpvestAllocationEntry>>(), Arg.Any<CancellationToken>())
            .Returns("alloc-1");
        _upvest.CreateBankTransactionAsync(Arg.Any<UpvestBankTransaction>(), Arg.Any<CancellationToken>())
            .Returns("bt1");
        _upvest.PlaceOrderAsync("u1", "a1", 100m, "EUR", Arg.Any<CancellationToken>())
            .Returns(new UpvestOrder("o1", "FILLED"));

        await _service.SignUpInvestorAsync("shopper-1", SampleSignUp());
        await _service.UpsertModelAsync("balanced", "Balanced",
            new List<ModelFundInput> { new("IE00B4L5Y983", 100) });
        await _service.ChooseModelAsync("shopper-1", "balanced");

        var payment = await _service.PayInAsync("shopper-1", 100m);

        Assert.Equal(PaymentStatus.Invested, payment.Status);
        Assert.Equal("o1", payment.UpvestOrderId);
        await _upvest.Received(1).CreateBankTransactionAsync(Arg.Any<UpvestBankTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PayIn_without_a_chosen_model_is_refused()
    {
        ArrangeFullOnboarding();
        await _service.SignUpInvestorAsync("shopper-1", SampleSignUp());

        await Assert.ThrowsAsync<InvestorNotReadyException>(() => _service.PayInAsync("shopper-1", 50m));
    }
}
