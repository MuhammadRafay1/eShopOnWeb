using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;

namespace PublicApiIntegrationTests.Portfolio;

/// <summary>
/// Deterministic in-memory stand-in for the Upvest API, so endpoint behaviour can be tested without
/// network access or credentials. Defaults to a fully-onboarded, active investor and a filled order.
/// </summary>
public class FakeUpvestClient : IUpvestClient
{
    public string UserStatus { get; set; } = "ACTIVE";
    public string AccountStatus { get; set; } = "ACTIVE";
    public string OrderStatus { get; set; } = "FILLED";
    public string RebalancingOrderStatus { get; set; } = "FILLED";
    public decimal ValuationTotal { get; set; } = 150.00m;

    public Task<UpvestResource> CreateUserAsync(InvestorSignup signup, CancellationToken ct = default)
        => Task.FromResult(new UpvestResource("user-1", UserStatus));

    public Task<UpvestResource?> GetUserAsync(string userId, CancellationToken ct = default)
        => Task.FromResult<UpvestResource?>(new UpvestResource(userId, UserStatus));

    public Task<UpvestResource> CreateUserIdentifierAsync(string userId, IdentityDocument document, CancellationToken ct = default)
        => Task.FromResult(new UpvestResource("identifier-1", null));

    public Task<string> CreateKycCheckAsync(string userId, InvestorSignup signup, CancellationToken ct = default)
        => Task.FromResult("check-1");

    public Task SetTaxResidenciesAsync(string userId, string taxCountry, string taxId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<UpvestResource> CreateAccountGroupAsync(string userId, CancellationToken ct = default)
        => Task.FromResult(new UpvestResource("account-group-1", "ACTIVE"));

    public Task<UpvestResource> CreateAccountAsync(string userId, string accountGroupId, CancellationToken ct = default)
        => Task.FromResult(new UpvestResource("account-1", AccountStatus));

    public Task<UpvestResource?> GetAccountAsync(string accountId, CancellationToken ct = default)
        => Task.FromResult<UpvestResource?>(new UpvestResource(accountId, AccountStatus));

    public Task<UpvestAllocation> CreateAllocationAsync(string name, IEnumerable<ModelFund> funds, CancellationToken ct = default)
        => Task.FromResult(ToAllocation(name, funds));

    public Task<UpvestAllocation> UpdateAllocationAsync(string allocationId, string name, IEnumerable<ModelFund> funds, CancellationToken ct = default)
        => Task.FromResult(ToAllocation(name, funds, allocationId));

    public Task<IReadOnlyList<UpvestAllocation>> ListAllocationsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UpvestAllocation>>(new List<UpvestAllocation>());

    public Task SetConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<UpvestResource> CreateTopupAsync(string accountGroupId, decimal amount, string currency, CancellationToken ct = default)
        => Task.FromResult(new UpvestResource("topup-1", "SETTLED"));

    public Task<UpvestResource?> GetTopupAsync(string topupId, CancellationToken ct = default)
        => Task.FromResult<UpvestResource?>(new UpvestResource(topupId, "SETTLED"));

    public Task<UpvestResource> CreatePortfolioOrderAsync(string userId, string accountId, decimal amount, string currency, CancellationToken ct = default)
        => Task.FromResult(new UpvestResource("order-1", OrderStatus));

    public Task<UpvestResource?> GetPortfolioOrderAsync(string orderId, CancellationToken ct = default)
        => Task.FromResult<UpvestResource?>(new UpvestResource(orderId, OrderStatus));

    public Task<string> TriggerRebalancingAsync(string allocationId, CancellationToken ct = default)
        => Task.FromResult("execution-1");

    public Task<IReadOnlyList<UpvestRebalancingOrder>> GetRebalancingOrdersAsync(string executionId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UpvestRebalancingOrder>>(new List<UpvestRebalancingOrder>
        {
            new("rebalancing-order-1", RebalancingOrderStatus)
        });

    public Task<UpvestValuation?> GetCurrentValuationAsync(string accountId, CancellationToken ct = default)
        => Task.FromResult<UpvestValuation?>(new UpvestValuation(
            ValuationTotal, "EUR",
            new List<UpvestPosition> { new("IE00B4L5Y983", 1.5m, ValuationTotal, "EUR") }));

    private static UpvestAllocation ToAllocation(string name, IEnumerable<ModelFund> funds, string id = "allocation-1")
        => new(id, name, funds.Select(f => new UpvestAllocationFund(f.InstrumentId, "ISIN", f.Weight.ToString("0.#####"))).ToList());
}
