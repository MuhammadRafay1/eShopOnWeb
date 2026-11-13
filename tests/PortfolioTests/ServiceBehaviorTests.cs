using System.Linq;
using System.Net;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.eShopWeb.Infrastructure.Portfolio;
using Microsoft.eShopWeb.Infrastructure.Portfolio.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.eShopWeb.PortfolioTests;

public class ServiceBehaviorTests
{
    private static Func<HttpRequestMessage, HttpResponseMessage> NeverCalled =>
        _ => throw new Xunit.Sdk.XunitException("No Upvest call should have been made.");

    private static Func<HttpRequestMessage, HttpResponseMessage> Queued(params string[] json)
    {
        var q = new Queue<string>(json);
        return _ => StubHandler.Json(HttpStatusCode.OK, q.Count > 0 ? q.Dequeue() : "{}");
    }

    // ---- Model validation happens before any provider call ----

    [Fact]
    public async Task Upsert_model_rejects_weights_that_do_not_sum_to_100()
    {
        var (client, _) = StubUpvest.BuildClient(NeverCalled);
        var svc = new ModelPortfolioService(client, StubUpvest.NewContext(), NullLogger<ModelPortfolioService>.Instance);

        await Assert.ThrowsAsync<PortfolioValidationException>(() => svc.UpsertModelAsync(
            "m", "M", new[] { new FundWeight("IE00B4L5Y983", 60m), new FundWeight("IE00B3RBWM25", 30m) }, default));
    }

    [Fact]
    public async Task Upsert_model_rejects_a_malformed_isin()
    {
        var (client, _) = StubUpvest.BuildClient(NeverCalled);
        var svc = new ModelPortfolioService(client, StubUpvest.NewContext(), NullLogger<ModelPortfolioService>.Instance);

        await Assert.ThrowsAsync<PortfolioValidationException>(() => svc.UpsertModelAsync(
            "m", "M", new[] { new FundWeight("NOT-AN-ISIN", 100m) }, default));
    }

    [Fact]
    public async Task Upsert_model_persists_funds_and_backs_it_with_a_upvest_allocation()
    {
        var allocationId = Guid.NewGuid();
        var alloc = $$"""
            {"id":"{{allocationId}}","created_at":"2025-01-01T00:00:00Z","updated_at":"2025-01-01T00:00:00Z",
             "name":"Balanced","allocation":[{"instrument_id":"IE00B4L5Y983","instrument_id_type":"ISIN","weight":"100"}]}
            """;
        var db = StubUpvest.NewContext();
        var (client, _) = StubUpvest.BuildClient(Queued(alloc));
        var svc = new ModelPortfolioService(client, db, NullLogger<ModelPortfolioService>.Instance);

        var result = await svc.UpsertModelAsync("balanced", "Balanced", new[] { new FundWeight("IE00B4L5Y983", 100m) }, default);

        Assert.Equal("balanced", result.ModelId);
        Assert.Single(result.Funds);
        var stored = db.Models.Single();
        Assert.Equal(allocationId, stored.AllocationId);
    }

    // ---- Pay-in preconditions (refuse before any provider call) ----

    [Fact]
    public async Task Pay_in_is_refused_when_the_caller_is_not_an_investor()
    {
        var (client, _) = StubUpvest.BuildClient(NeverCalled);
        var svc = new PaymentService(client, StubUpvest.NewContext(), NullLogger<PaymentService>.Instance);

        await Assert.ThrowsAsync<PortfolioConflictException>(() => svc.PayInAsync("shopper-1", 100m, default));
    }

    [Fact]
    public async Task Pay_in_is_refused_when_no_model_has_been_chosen()
    {
        var db = StubUpvest.NewContext();
        db.Investors.Add(new Investor
        {
            ShopperId = "shopper-1",
            UpvestUserId = Guid.NewGuid(),
            AccountGroupId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Status = InvestorSignupStatus.Active
            // ChosenModelId is null -> no model chosen
        });
        await db.SaveChangesAsync();

        var (client, _) = StubUpvest.BuildClient(NeverCalled);
        var svc = new PaymentService(client, db, NullLogger<PaymentService>.Instance);

        await Assert.ThrowsAsync<PortfolioConflictException>(() => svc.PayInAsync("shopper-1", 100m, default));
    }

    [Fact]
    public async Task Pay_in_credits_and_invests_then_reports_invested()
    {
        var db = StubUpvest.NewContext();
        db.Investors.Add(new Investor
        {
            ShopperId = "shopper-1",
            UpvestUserId = Guid.NewGuid(),
            AccountGroupId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            ChosenModelId = "balanced",
            Status = InvestorSignupStatus.Active
        });
        await db.SaveChangesAsync();

        var topup = """{"id":"11111111-1111-1111-1111-111111111111","created_at":"2025-01-01T00:00:00Z","account_group_id":"22222222-2222-2222-2222-222222222222","cash_amount":"250.00","currency":"EUR","status":"SETTLED"}""";
        var order = """{"id":"33333333-3333-3333-3333-333333333333","created_at":"2025-01-01T00:00:00Z","updated_at":"2025-01-01T00:00:00Z","account_id":"44444444-4444-4444-4444-444444444444","cash_amount":"250.00","currency":"EUR","status":"FILLED","orders":[],"client_reference":"ref-1","initiation_flow":"PORTFOLIO_REBALANCING"}""";
        var (client, handler) = StubUpvest.BuildClient(Queued(topup, order));
        var svc = new PaymentService(client, db, NullLogger<PaymentService>.Instance);

        var result = await svc.PayInAsync("shopper-1", 250m, default);

        Assert.Equal(PaymentStatus.Invested, result.Status);
        Assert.Equal(250m, result.Amount);
        var stored = db.Payments.Single();
        Assert.Equal(PaymentStatus.Invested, stored.Status);
        Assert.NotNull(stored.TopUpId);
        Assert.NotNull(stored.OrderId);
        // The invest order serialized as a BUY for the two-decimal euro amount.
        Assert.Contains(handler.Bodies, b => b is not null && b.Contains("\"side\":\"BUY\"") && b.Contains("\"cash_amount\":\"250.00\""));
    }

    [Fact]
    public async Task Pay_in_reports_failed_when_the_provider_rejects_it()
    {
        var db = StubUpvest.NewContext();
        db.Investors.Add(new Investor
        {
            ShopperId = "shopper-1",
            UpvestUserId = Guid.NewGuid(),
            AccountGroupId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            ChosenModelId = "balanced",
            Status = InvestorSignupStatus.Active
        });
        await db.SaveChangesAsync();

        var (client, _) = StubUpvest.BuildClient(_ => StubHandler.Json(HttpStatusCode.BadRequest, """{"detail":"nope"}"""));
        var svc = new PaymentService(client, db, NullLogger<PaymentService>.Instance);

        var result = await svc.PayInAsync("shopper-1", 100m, default);

        Assert.Equal(PaymentStatus.Failed, result.Status);
        Assert.Equal(PaymentStatus.Failed, db.Payments.Single().Status);
    }

    // ---- Realignment preconditions ----

    [Fact]
    public async Task Realignment_of_an_unknown_model_is_not_found()
    {
        var (client, _) = StubUpvest.BuildClient(NeverCalled);
        var svc = new RealignmentService(client, StubUpvest.NewContext(), NullLogger<RealignmentService>.Instance);

        await Assert.ThrowsAsync<PortfolioNotFoundException>(() => svc.TriggerAsync("ghost", default));
    }

    [Fact]
    public async Task Realignment_is_refused_when_the_model_has_no_allocation_yet()
    {
        var db = StubUpvest.NewContext();
        db.Models.Add(new ModelPortfolio { ModelId = "balanced", Name = "Balanced", AllocationId = null });
        await db.SaveChangesAsync();

        var (client, _) = StubUpvest.BuildClient(NeverCalled);
        var svc = new RealignmentService(client, db, NullLogger<RealignmentService>.Instance);

        await Assert.ThrowsAsync<PortfolioConflictException>(() => svc.TriggerAsync("balanced", default));
    }

    // ---- Investor sign-up is idempotent per shopper ----

    [Fact]
    public async Task Choosing_a_model_is_refused_before_sign_up()
    {
        var (client, _) = StubUpvest.BuildClient(NeverCalled);
        var svc = new ModelPortfolioService(client, StubUpvest.NewContext(), NullLogger<ModelPortfolioService>.Instance);

        await Assert.ThrowsAsync<PortfolioConflictException>(() => svc.ChooseModelAsync("shopper-1", "balanced", default));
    }
}
