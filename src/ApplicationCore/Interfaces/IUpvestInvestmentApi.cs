using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The surface of the Upvest Investment API this integration uses. Each method maps to one or
/// more signed, authenticated Upvest HTTP calls. Implementations translate wire shapes to the
/// domain records above and throw <see cref="UpvestApiException"/> on a non-success response.
/// </summary>
public interface IUpvestInvestmentApi
{
    // ---- Users / onboarding (Flow 1) ----
    Task<UpvestUser> CreateUserAsync(InvestorSignUp signUp, CancellationToken ct = default);
    Task SubmitKycCheckAsync(string userId, InvestorSignUp signUp, CancellationToken ct = default);
    Task SetTaxResidencyAsync(string userId, string taxCountry, string taxId, CancellationToken ct = default);
    Task<UpvestAccountGroup> CreateAccountGroupAsync(string userId, CancellationToken ct = default);
    Task<UpvestAccount> CreateAccountAsync(string userId, string accountGroupId, CancellationToken ct = default);
    Task<UpvestUser> GetUserAsync(string userId, CancellationToken ct = default);
    Task<UpvestAccount> GetAccountAsync(string accountId, CancellationToken ct = default);

    // ---- Model portfolios (Flow 2) ----
    Task<string> CreateAllocationAsync(string name, IReadOnlyList<FundWeightInput> funds, CancellationToken ct = default);
    Task ReplaceAllocationAsync(string allocationId, string name, IReadOnlyList<FundWeightInput> funds, CancellationToken ct = default);
    Task CreateConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default);
    Task UpdateConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default);

    // ---- Paying in (Flow 3) ----
    /// <summary>Credit the account group with cash using the Upvest sandbox funding facility.</summary>
    Task FundAccountAsync(string accountGroupId, decimal amount, string currency, CancellationToken ct = default);
    Task<UpvestPortfolioOrder> CreatePortfolioOrderAsync(string userId, string accountId, decimal amount, string currency, CancellationToken ct = default);
    Task<UpvestPortfolioOrder> GetPortfolioOrderAsync(string portfolioOrderId, CancellationToken ct = default);

    // ---- Realignment (Flow 4) ----
    Task<string> CreateRebalancingAsync(string allocationId, CancellationToken ct = default);
    /// <summary>The statuses of the individual orders produced by a rebalancing execution.</summary>
    Task<IReadOnlyList<string>> GetRebalancingOrderStatusesAsync(string executionId, CancellationToken ct = default);

    // ---- Portfolio view (Flow 5) ----
    Task<UpvestValuation> GetValuationAsync(string accountId, CancellationToken ct = default);
}
