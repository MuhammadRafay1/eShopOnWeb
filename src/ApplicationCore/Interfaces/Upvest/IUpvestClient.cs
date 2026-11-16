using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;

/// <summary>
/// Port to the Upvest Investment API. The implementation handles OAuth2 token acquisition,
/// HTTP message signing, idempotency keys and wire (de)serialisation. All methods talk to the
/// base address configured in <c>Upvest:BaseUrl</c>.
/// Any non-success Upvest response is surfaced as <see cref="UpvestException"/>.
/// </summary>
public interface IUpvestClient
{
    // ---- Flow 1: becoming an investor (onboarding) ----
    Task<UpvestResource> CreateUserAsync(InvestorSignup signup, CancellationToken ct = default);
    Task<UpvestResource?> GetUserAsync(string userId, CancellationToken ct = default);
    Task<UpvestResource> CreateUserIdentifierAsync(string userId, IdentityDocument document, CancellationToken ct = default);
    Task<string> CreateKycCheckAsync(string userId, InvestorSignup signup, CancellationToken ct = default);
    Task SetTaxResidenciesAsync(string userId, string taxCountry, string taxId, CancellationToken ct = default);
    Task<UpvestResource> CreateAccountGroupAsync(string userId, CancellationToken ct = default);
    Task<UpvestResource> CreateAccountAsync(string userId, string accountGroupId, CancellationToken ct = default);
    Task<UpvestResource?> GetAccountAsync(string accountId, CancellationToken ct = default);

    // ---- Flow 2: model portfolios (allocations + configurations) ----
    Task<UpvestAllocation> CreateAllocationAsync(string name, IEnumerable<ModelFund> funds, CancellationToken ct = default);
    Task<UpvestAllocation> UpdateAllocationAsync(string allocationId, string name, IEnumerable<ModelFund> funds, CancellationToken ct = default);
    Task<IReadOnlyList<UpvestAllocation>> ListAllocationsAsync(CancellationToken ct = default);
    Task SetConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default);

    // ---- Flow 3: paying in (funding + portfolio order) ----
    Task<UpvestResource> CreateTopupAsync(string accountGroupId, decimal amount, string currency, CancellationToken ct = default);
    Task<UpvestResource?> GetTopupAsync(string topupId, CancellationToken ct = default);
    Task<UpvestResource> CreatePortfolioOrderAsync(string userId, string accountId, decimal amount, string currency, CancellationToken ct = default);
    Task<UpvestResource?> GetPortfolioOrderAsync(string orderId, CancellationToken ct = default);

    // ---- Flow 4: realignment (rebalancing) ----
    Task<string> TriggerRebalancingAsync(string allocationId, CancellationToken ct = default);
    Task<IReadOnlyList<UpvestRebalancingOrder>> GetRebalancingOrdersAsync(string executionId, CancellationToken ct = default);

    // ---- Flow 5: the investor's portfolio (valuation + positions) ----
    Task<UpvestValuation?> GetCurrentValuationAsync(string accountId, CancellationToken ct = default);
}
