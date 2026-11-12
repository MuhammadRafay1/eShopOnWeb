using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Portfolios;
using Microsoft.eShopWeb.Infrastructure.Portfolios;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Portfolios;

/// <summary>
/// Unit tests for the managed-portfolio orchestration. The Upvest client is mocked so the tests are
/// hermetic; the real in-memory stores are reused to exercise persistence behaviour.
/// </summary>
public class PortfolioServiceTests
{
    private readonly IUpvestInvestmentClient _upvest = Substitute.For<IUpvestInvestmentClient>();
    private readonly InMemoryInvestorStore _investors = new();
    private readonly InMemoryModelPortfolioStore _models = new();
    private readonly InMemoryPaymentStore _payments = new();
    private readonly InMemoryRealignmentStore _realignments = new();

    public PortfolioServiceTests()
    {
        // Sensible defaults so sign-up's account-group/account steps don't dereference null in tests
        // that don't care about them. Individual tests override these where it matters.
        _upvest.CreateAccountGroupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestAccountGroup("group-1", "PENDING_APPROVAL"));
        _upvest.CreatePortfolioAccountAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestAccount("acct-1", "PENDING_APPROVAL"));
    }

    private PortfolioService NewService() => new(_upvest, _investors, _models, _payments, _realignments);

    private static InvestorSignUp SampleForm() => new(
        "Alex", "Muster", "alex@example.com", new DateOnly(1990, 5, 1), "Berlin", "DE", "DE",
        new UpvestAddress("Rosenweg 221", "10115", "Berlin", "DE"), "+49123456789",
        "12345678901", "DE",
        new UpvestIdentityDocument("PASSPORT", "AB1234567", "DE", new DateOnly(2030, 1, 1)));

    [Fact]
    public async Task SignUp_CreatesUser_AndStartsPending()
    {
        _upvest.CreateUserAsync(Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestUser("user-1", "INACTIVE"));
        _upvest.CreateAccountGroupAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new UpvestAccountGroup("group-1", "PENDING_APPROVAL"));
        _upvest.CreatePortfolioAccountAsync("user-1", "group-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestAccount("acct-1", "PENDING_APPROVAL"));

        var service = NewService();
        var investor = await service.SignUpInvestorAsync("shopper@x.com", SampleForm());

        Assert.Equal("user-1", investor.UpvestUserId);
        Assert.Equal("acct-1", investor.UpvestAccountId);
        Assert.Equal(InvestorStatus.Pending, investor.Status);
    }

    [Fact]
    public async Task SignUp_WhenUserActiveAndAccountActive_IsActive()
    {
        _upvest.CreateUserAsync(Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestUser("user-1", "ACTIVE"));
        _upvest.CreateAccountGroupAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new UpvestAccountGroup("group-1", "ACTIVE"));
        _upvest.CreatePortfolioAccountAsync("user-1", "group-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestAccount("acct-1", "ACTIVE"));

        var investor = await NewService().SignUpInvestorAsync("shopper@x.com", SampleForm());

        Assert.Equal(InvestorStatus.Active, investor.Status);
    }

    [Fact]
    public async Task SignUp_Twice_IsConflict()
    {
        _upvest.CreateUserAsync(Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestUser("user-1", "INACTIVE"));
        var service = NewService();
        await service.SignUpInvestorAsync("shopper@x.com", SampleForm());

        await Assert.ThrowsAsync<PortfolioConflictException>(
            () => service.SignUpInvestorAsync("shopper@x.com", SampleForm()));
    }

    [Fact]
    public async Task SignUp_SkipsIdentifier_ForConcatNationality()
    {
        _upvest.CreateUserAsync(Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestUser("user-1", "INACTIVE"));
        await NewService().SignUpInvestorAsync("shopper@x.com", SampleForm()); // DE is a CONCAT nationality

        await _upvest.DidNotReceive().CreatePassportIdentifierAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertModel_RejectsWeightsNotSummingTo100()
    {
        var funds = new List<ModelFundInput> { new("IE00B4L5Y983", 60), new("IE00B3RBWM25", 30) };
        await Assert.ThrowsAsync<PortfolioValidationException>(
            () => NewService().UpsertModelAsync("balanced", "Balanced", funds));
    }

    [Fact]
    public async Task UpsertModel_CreatesAllocation_WithFractionWeights()
    {
        _upvest.CreateAllocationAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UpvestAllocationFund>>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestAllocation("alloc-1", "Balanced", Array.Empty<UpvestAllocationFund>()));

        var funds = new List<ModelFundInput> { new("IE00B4L5Y983", 60), new("IE00B3RBWM25", 40) };
        var model = await NewService().UpsertModelAsync("balanced", "Balanced", funds);

        Assert.Equal("alloc-1", model.UpvestAllocationId);
        await _upvest.Received().CreateAllocationAsync("Balanced",
            Arg.Is<IReadOnlyList<UpvestAllocationFund>>(a => a.Count == 2 && a[0].Weight == 0.6m && a[1].Weight == 0.4m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PayIn_WhenNotInvestor_IsNotAnInvestor()
    {
        await Assert.ThrowsAsync<NotAnInvestorException>(
            () => NewService().PayInAsync("nobody@x.com", 100m));
    }

    [Fact]
    public async Task PayIn_WhenNotActive_IsConflict()
    {
        await _investors.AddAsync(new Investor
        {
            ShopperId = "s@x.com",
            UpvestUserId = "user-1",
            Status = InvestorStatus.Pending,
            ModelId = "balanced"
        });
        _upvest.GetUserAsync("user-1", Arg.Any<CancellationToken>()).Returns(new UpvestUser("user-1", "INACTIVE"));

        await Assert.ThrowsAsync<PortfolioConflictException>(() => NewService().PayInAsync("s@x.com", 100m));
    }

    [Fact]
    public async Task PayIn_WhenActive_CreditsAndInvests()
    {
        await _investors.AddAsync(new Investor
        {
            ShopperId = "s@x.com",
            UpvestUserId = "user-1",
            UpvestAccountGroupId = "group-1",
            UpvestAccountId = "acct-1",
            ModelId = "balanced",
            Status = InvestorStatus.Active
        });
        _upvest.GetUserAsync("user-1", Arg.Any<CancellationToken>()).Returns(new UpvestUser("user-1", "ACTIVE"));
        _upvest.GetAccountAsync("acct-1", Arg.Any<CancellationToken>()).Returns(new UpvestAccount("acct-1", "ACTIVE"));
        _upvest.CreateTopupAsync("group-1", 100m, "EUR", Arg.Any<CancellationToken>()).Returns(new UpvestTopup("top-1", "CONFIRMED"));
        _upvest.CreateBuyOrderAsync("user-1", "acct-1", 100m, "EUR", Arg.Any<CancellationToken>())
            .Returns(new UpvestPortfolioOrder("order-1", "NEW"));

        var payment = await NewService().PayInAsync("s@x.com", 100m);

        Assert.Equal("top-1", payment.UpvestTopupId);
        Assert.Equal("order-1", payment.UpvestPortfolioOrderId);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public async Task PayIn_RejectsMoreThanTwoDecimals()
    {
        await _investors.AddAsync(new Investor { ShopperId = "s@x.com", UpvestUserId = "u", Status = InvestorStatus.Active, ModelId = "m" });
        _upvest.GetUserAsync("u", Arg.Any<CancellationToken>()).Returns(new UpvestUser("u", "ACTIVE"));
        await Assert.ThrowsAsync<PortfolioValidationException>(() => NewService().PayInAsync("s@x.com", 100.123m));
    }

    [Fact]
    public async Task ListPayments_RefreshesStatusFromUpvest()
    {
        await _payments.AddAsync(new Payment { ShopperId = "s@x.com", Amount = 100m, UpvestPortfolioOrderId = "order-1", Status = PaymentStatus.Pending });
        _upvest.GetPortfolioOrderAsync("order-1", Arg.Any<CancellationToken>()).Returns(new UpvestPortfolioOrder("order-1", "FILLED"));

        var payments = await NewService().ListPaymentsAsync("s@x.com");

        Assert.Single(payments);
        Assert.Equal(PaymentStatus.Invested, payments[0].Status);
    }

    [Fact]
    public async Task GetRealignment_DerivesCompletedFromOrders()
    {
        var realignment = await _realignments.AddAsync(new Realignment { ModelId = "m", UpvestExecutionId = "exec-1", Status = RealignmentStatus.Pending });
        _upvest.GetRebalancingOrderStatusesAsync("exec-1", Arg.Any<CancellationToken>())
            .Returns(new List<string> { "FILLED", "FILLED" });

        var result = await NewService().GetRealignmentAsync(realignment.Id);

        Assert.NotNull(result);
        Assert.Equal(RealignmentStatus.Completed, result!.Status);
    }

    [Fact]
    public async Task GetPortfolio_SumsPaidInAndReadsValuation()
    {
        await _investors.AddAsync(new Investor { ShopperId = "s@x.com", UpvestUserId = "u", UpvestAccountId = "acct-1", ModelId = "balanced", Status = InvestorStatus.Active });
        await _payments.AddAsync(new Payment { ShopperId = "s@x.com", Amount = 100m, Status = PaymentStatus.Invested });
        await _payments.AddAsync(new Payment { ShopperId = "s@x.com", Amount = 50m, Status = PaymentStatus.Failed });
        _upvest.GetCurrentValuationAsync("acct-1", Arg.Any<CancellationToken>())
            .Returns(new UpvestValuation(new List<UpvestHolding> { new("IE00B4L5Y983", 1.5m, 160m) }, 160m, "EUR"));

        var view = await NewService().GetPortfolioAsync("s@x.com");

        Assert.NotNull(view);
        Assert.Equal("balanced", view!.ModelId);
        Assert.Equal(100m, view.PaidIn); // failed payment excluded
        Assert.Equal(160m, view.TotalValue);
        Assert.Single(view.Holdings);
    }
}
