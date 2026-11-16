using System.Net;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Xunit;

namespace Microsoft.eShopWeb.PortfolioTests;

/// <summary>
/// Exercises the gateway against a stubbed Upvest, proving each flow works end-to-end when the scopes are
/// available — the part the sandbox cannot demonstrate because its client is limited to user operations.
/// </summary>
public class UpvestPortfolioGatewayTests
{
    private static InvestorSignUp SampleForm() => new(
        "Erika", "Mustermann", "erika@example.com",
        new DateTimeOffset(1985, 4, 12, 0, 0, 0, TimeSpan.Zero),
        "Berlin", "DE", "DE",
        new SignUpAddress("Alexanderplatz 1", "10178", "Berlin", "DE"),
        "491512345678", "26954371827", "DE",
        new SignUpIdentityDocument("PASSPORT", "C01X00T47", "DE", new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task OnboardInvestor_runs_the_full_chain_and_returns_all_ids()
    {
        var userId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var (gateway, _) = UpvestTestClient.BuildGateway(
            (HttpStatusCode.OK, Payloads.UserTol(userId)),
            (HttpStatusCode.OK, Payloads.CheckCreated(checkId)),
            (HttpStatusCode.OK, Payloads.TaxRecord()),
            (HttpStatusCode.OK, Payloads.AccountGroup(groupId)),
            (HttpStatusCode.OK, Payloads.Account(accountId, groupId)));

        var keys = new InvestorOnboardingKeys(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var result = await gateway.OnboardInvestorAsync(SampleForm(), keys, CancellationToken.None);

        Assert.Equal(userId, result.UserId);
        Assert.Equal(checkId, result.CheckId);
        Assert.Equal(groupId, result.AccountGroupId);
        Assert.Equal(accountId, result.AccountId);
        Assert.Null(result.IncompleteReason);
    }

    [Theory]
    [InlineData("ACTIVE", "ACTIVE", InvestorStatus.Active)]
    [InlineData("INACTIVE", "PENDING_APPROVAL", InvestorStatus.Pending)]
    [InlineData("ACTIVE", "PENDING_APPROVAL", InvestorStatus.Pending)]
    public async Task GetInvestorStatus_derives_from_user_and_account(string userStatus, string accountStatus, InvestorStatus expected)
    {
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var (gateway, _) = UpvestTestClient.BuildGateway(
            (HttpStatusCode.OK, Payloads.UserTol(userId, userStatus)),
            (HttpStatusCode.OK, Payloads.Account(accountId, Guid.NewGuid(), accountStatus)));

        var status = await gateway.GetInvestorStatusAsync(userId, accountId, CancellationToken.None);
        Assert.Equal(expected, status);
    }

    [Fact]
    public async Task GetInvestorStatus_offboarded_user_is_rejected()
    {
        var userId = Guid.NewGuid();
        var (gateway, _) = UpvestTestClient.BuildGateway(
            (HttpStatusCode.OK, Payloads.UserTol(userId, "OFFBOARDED")));

        var status = await gateway.GetInvestorStatusAsync(userId, Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(InvestorStatus.Rejected, status);
    }

    [Fact]
    public async Task CreateModel_maps_the_allocation_and_sends_weights_as_percentages()
    {
        var allocationId = Guid.NewGuid();
        var (gateway, handler) = UpvestTestClient.BuildGateway((HttpStatusCode.OK, Payloads.Allocation(allocationId)));

        var funds = new List<FundWeight> { new("IE00B4L5Y983", 60m), new("IE00B3RBWM25", 40m) };
        var result = await gateway.CreateModelAsync("Balanced", funds, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(allocationId, result.AllocationId);
        Assert.Equal("Balanced", result.Name);
        Assert.Equal(2, result.Funds.Count);
        Assert.Equal(60m, result.Funds[0].Weight);

        // The outgoing allocation body carries weights as whole-number percentages, not fractions.
        var allocationBody = handler.Bodies.Last(b => b is not null && b.Contains("allocation"));
        Assert.Contains("\"weight\":\"60\"", allocationBody);
        Assert.Contains("\"instrument_id\":\"IE00B4L5Y983\"", allocationBody);
    }

    [Fact]
    public async Task ListModels_maps_every_allocation()
    {
        var (gateway, _) = UpvestTestClient.BuildGateway((HttpStatusCode.OK, Payloads.AllocationsList(Guid.NewGuid())));
        var models = await gateway.ListModelsAsync(CancellationToken.None);
        Assert.Single(models);
        Assert.Equal(2, models[0].Funds.Count);
    }

    [Theory]
    [InlineData(RealignmentStatus.Completed, "FILLED", "FILLED")]
    [InlineData(RealignmentStatus.Pending, "PROCESSING", "FILLED")]
    [InlineData(RealignmentStatus.Failed, "CANCELLED", "FILLED")]
    public async Task GetRealignmentStatus_is_derived_from_execution_orders(RealignmentStatus expected, params string[] statuses)
    {
        var (gateway, _) = UpvestTestClient.BuildGateway((HttpStatusCode.OK, Payloads.ExecutionOrders(statuses)));
        var status = await gateway.GetRealignmentStatusAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(expected, status);
    }

    [Fact]
    public async Task GetPortfolio_maps_holdings_and_total()
    {
        var accountId = Guid.NewGuid();
        var (gateway, _) = UpvestTestClient.BuildGateway((HttpStatusCode.OK, Payloads.Valuation(accountId)));

        var snapshot = await gateway.GetPortfolioAsync(accountId, CancellationToken.None);

        Assert.Equal(1000.00m, snapshot.TotalValue);
        Assert.Equal(2, snapshot.Holdings.Count);
        Assert.Equal("IE00B4L5Y983", snapshot.Holdings[0].InstrumentId);
        Assert.Equal(10.5m, snapshot.Holdings[0].Quantity);
        Assert.Equal(600.00m, snapshot.Holdings[0].Value);
    }

    [Fact]
    public async Task PayIn_credits_then_invests_and_reports_invested()
    {
        var orderId = Guid.NewGuid();
        var (gateway, handler) = UpvestTestClient.BuildGateway(
            (HttpStatusCode.OK, Payloads.UserTol(Guid.NewGuid())),       // owner name read-back
            (HttpStatusCode.OK, Payloads.BankTransaction(Guid.NewGuid())), // sandbox credit
            (HttpStatusCode.OK, Payloads.Order(orderId, "FILLED")));       // the BUY order

        var result = await gateway.PayInAsync(
            userId: Guid.NewGuid(),
            accountId: Guid.NewGuid(),
            accountGroupId: Guid.NewGuid(),
            existingVirtualBankAccountId: Guid.NewGuid(), // skip vIBAN creation
            virtualBankAccountIdempotencyKey: Guid.NewGuid(),
            amount: 100.00m,
            orderIdempotencyKey: Guid.NewGuid(),
            clientReference: "ref123",
            CancellationToken.None);

        Assert.Equal(orderId, result.PortfolioOrderId);
        Assert.Equal(PaymentStatus.Invested, result.Status);

        // A credit (cash_amount "100.00") and a BUY order (side) were both sent.
        Assert.Contains(handler.Bodies, b => b is not null && b.Contains("\"cash_amount\":\"100.00\""));
        Assert.Contains(handler.Bodies, b => b is not null && b.Contains("\"side\":\"BUY\""));
    }

    [Fact]
    public async Task OnboardInvestor_reconciles_an_unknown_CreateUser_outcome_by_replaying_the_key()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var handler = new StubHandler(new[]
        {
            (HttpStatusCode.OK, Payloads.UserTol(userId)),       // the replay (after the dropped connection)
            (HttpStatusCode.OK, Payloads.CheckCreated(Guid.NewGuid())),
            (HttpStatusCode.OK, Payloads.TaxRecord()),
            (HttpStatusCode.OK, Payloads.AccountGroup(groupId)),
            (HttpStatusCode.OK, Payloads.Account(accountId, groupId)),
        })
        {
            ThrowFirst = 1, // the first CreateUser POST drops its connection ⇒ outcome unknown
        };

        var gateway = UpvestTestClient.BuildGatewayWith(handler);
        var keys = new InvestorOnboardingKeys(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var result = await gateway.OnboardInvestorAsync(SampleForm(), keys, CancellationToken.None);

        Assert.Equal(userId, result.UserId);          // reconciled by replaying the same idempotency key
        Assert.Equal(accountId, result.AccountId);
    }

    [Fact]
    public async Task A_scope_denied_response_is_an_authorization_issue()
    {
        var gateway = UpvestTestClient.BuildGatewayWith(StubHandler.AlwaysStatus(HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<UpvestGatewayException>(
            () => gateway.GetRealignmentStatusAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.True(ex.IsAuthorizationScopeIssue);
        Assert.Equal(401, ex.StatusCode);
    }
}
