using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.Infrastructure.Portfolio;
using Xunit;

namespace Microsoft.eShopWeb.PortfolioTests;

public class InMemoryPortfolioStoreTests
{
    [Fact]
    public void TryCreateInvestor_refuses_a_second_investor_for_the_same_shopper()
    {
        var store = new InMemoryPortfolioStore();
        Assert.True(store.TryCreateInvestor(new Investor("buyer-1")));
        Assert.False(store.TryCreateInvestor(new Investor("buyer-1"))); // duplicate claim refused
        Assert.True(store.TryCreateInvestor(new Investor("buyer-2")));
    }

    [Fact]
    public void Investors_are_isolated_per_shopper()
    {
        var store = new InMemoryPortfolioStore();
        store.TryCreateInvestor(new Investor("buyer-1"));
        Assert.NotNull(store.GetInvestor("buyer-1"));
        Assert.Null(store.GetInvestor("buyer-2"));
    }

    [Fact]
    public void TryClaimPayment_refuses_a_duplicate_idempotency_key_and_returns_the_existing()
    {
        var store = new InMemoryPortfolioStore();
        var key = Guid.NewGuid();
        var first = new InvestorPayment("buyer-1", 100m, key);

        Assert.True(store.TryClaimPayment(first, out _));

        var retry = new InvestorPayment("buyer-1", 100m, key);
        Assert.False(store.TryClaimPayment(retry, out var existing));
        Assert.Equal(first.Id, existing.Id); // the original payment, not a duplicate
    }

    [Fact]
    public void A_different_shopper_may_reuse_an_idempotency_key()
    {
        var store = new InMemoryPortfolioStore();
        var key = Guid.NewGuid();
        Assert.True(store.TryClaimPayment(new InvestorPayment("buyer-1", 10m, key), out _));
        Assert.True(store.TryClaimPayment(new InvestorPayment("buyer-2", 10m, key), out _));
    }

    [Fact]
    public void ListPayments_returns_newest_first_and_only_the_owner_s()
    {
        var store = new InMemoryPortfolioStore();
        var older = new InvestorPayment("buyer-1", 10m, Guid.NewGuid());
        store.TryClaimPayment(older, out _);
        Thread.Sleep(5);
        var newer = new InvestorPayment("buyer-1", 20m, Guid.NewGuid());
        store.TryClaimPayment(newer, out _);
        store.TryClaimPayment(new InvestorPayment("buyer-2", 99m, Guid.NewGuid()), out _);

        var payments = store.ListPayments("buyer-1");
        Assert.Equal(2, payments.Count);
        Assert.Equal(newer.Id, payments[0].Id); // newest first
    }

    [Fact]
    public void RemovePayment_releases_the_claim_so_the_key_can_be_reused()
    {
        var store = new InMemoryPortfolioStore();
        var key = Guid.NewGuid();
        var payment = new InvestorPayment("buyer-1", 10m, key);
        store.TryClaimPayment(payment, out _);

        store.RemovePayment("buyer-1", payment.Id);

        Assert.Empty(store.ListPayments("buyer-1"));
        Assert.True(store.TryClaimPayment(new InvestorPayment("buyer-1", 10m, key), out _)); // key free again
    }

    [Fact]
    public void Models_round_trip_through_the_catalog()
    {
        var store = new InMemoryPortfolioStore();
        var model = new ModelPortfolio("balanced", "Balanced", new[] { new ModelPortfolioFund("IE00B4L5Y983", 100m) })
        {
            AllocationId = Guid.NewGuid(),
        };
        store.UpsertModel(model);

        Assert.Equal("Balanced", store.GetModel("balanced")!.Name);
        Assert.Single(store.ListModels());
    }
}
