using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Upvest;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// A thin, typed gateway over the Upvest Investment API. The implementation is responsible for
/// OAuth client-credentials tokens and HTTP message signing; callers work in domain terms.
/// <para>
/// All calls talk to Upvest's sandbox/live host (<c>Upvest:BaseUrl</c>). A call whose scope the
/// client is not authorised for surfaces as <see cref="UpvestAuthorizationException"/>; other
/// failures surface as <see cref="UpvestApiException"/>. Neither exception carries personal data.
/// </para>
/// </summary>
public interface IUpvestClient
{
    // ----- Users (investor onboarding) -----
    Task<UpvestUser> CreateUserAsync(UpvestCreateUserRequest request, CancellationToken ct = default);
    Task<UpvestUser?> GetUserAsync(string userId, CancellationToken ct = default);
    Task<string> CreateKycCheckAsync(string userId, UpvestKycCheckRequest request, CancellationToken ct = default);
    Task<UpvestCheck?> GetCheckAsync(string userId, string checkId, CancellationToken ct = default);

    // ----- Accounts -----
    Task<UpvestAccountGroup> CreateAccountGroupAsync(string userId, CancellationToken ct = default);
    Task<UpvestAccount> CreateAccountAsync(string userId, string accountGroupId, CancellationToken ct = default);
    Task<UpvestAccount?> GetAccountAsync(string accountId, CancellationToken ct = default);

    // ----- Model portfolios (allocations) and the investor's configuration -----
    Task<UpvestAllocation> CreateAllocationAsync(string name, IReadOnlyList<UpvestAllocationEntry> funds, CancellationToken ct = default);
    Task<UpvestAllocation> UpdateAllocationAsync(string allocationId, string name, IReadOnlyList<UpvestAllocationEntry> funds, CancellationToken ct = default);
    Task CreateConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default);
    Task UpdateConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default);

    // ----- Paying in -----
    Task TopUpAsync(string accountGroupId, decimal amount, string currency, CancellationToken ct = default);
    Task<UpvestOrder> CreatePortfolioOrderAsync(string userId, string accountId, decimal amount, string currency, CancellationToken ct = default);
    Task<UpvestOrder?> GetPortfolioOrderAsync(string portfolioOrderId, CancellationToken ct = default);

    // ----- Realignment (rebalancing) -----
    Task<string> TriggerRebalancingAsync(string allocationId, CancellationToken ct = default);
    Task<UpvestRebalancing?> GetRebalancingAsync(string executionId, CancellationToken ct = default);

    // ----- Portfolio holdings & valuation -----
    Task<UpvestValuation?> GetAccountValuationAsync(string accountId, CancellationToken ct = default);
}
