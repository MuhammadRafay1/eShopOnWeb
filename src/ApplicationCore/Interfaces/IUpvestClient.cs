using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Abstraction over the Upvest Investment API. Every method performs an authenticated,
/// HTTP-message-signed call to Upvest. Implementations throw <see cref="UpvestApiException"/>
/// on non-success responses.
/// </summary>
public interface IUpvestClient
{
    // --- Onboarding (becoming an investor) ---
    Task<UpvestUser> CreateUserAsync(InvestorSignUp signUp, CancellationToken ct = default);
    Task<UpvestUser?> GetUserAsync(string userId, CancellationToken ct = default);
    Task SubmitKycCheckAsync(string userId, KycCheckInput check, CancellationToken ct = default);
    Task SubmitInstrumentFitCheckAsync(string userId, bool suitable, CancellationToken ct = default);
    Task CreateIdentifierAsync(string userId, string type, string issuingCountry, string identifierStandard, string identifier, CancellationToken ct = default);
    Task SetTaxResidenciesAsync(string userId, IReadOnlyList<TaxResidencyInput> residencies, CancellationToken ct = default);
    Task<UpvestAccountGroup> CreateAccountGroupAsync(string userId, string type, CancellationToken ct = default);
    Task<UpvestAccount> CreateAccountAsync(string userId, string accountGroupId, string type, CancellationToken ct = default);
    Task<UpvestAccount?> GetAccountAsync(string accountId, CancellationToken ct = default);

    // --- Model portfolios (allocations) ---
    Task<UpvestAllocation> CreateAllocationAsync(string name, IReadOnlyList<UpvestFund> funds, CancellationToken ct = default);
    Task<UpvestAllocation> ReplaceAllocationAsync(string allocationId, string name, IReadOnlyList<UpvestFund> funds, CancellationToken ct = default);
    Task<IReadOnlyList<UpvestAllocation>> ListAllocationsAsync(CancellationToken ct = default);

    // --- Portfolio configuration (an investor following a model) ---
    Task CreateConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default);
    Task UpdateConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default);
    Task<UpvestConfiguration?> GetConfigurationAsync(string accountId, CancellationToken ct = default);

    // --- Paying in ---
    Task IncreaseVirtualCashAsync(string accountGroupId, decimal amount, string currency, CancellationToken ct = default);
    Task<UpvestPortfolioOrder> CreatePortfolioOrderAsync(string userId, string accountId, decimal cashAmount, string currency, CancellationToken ct = default);
    Task<UpvestPortfolioOrder?> GetPortfolioOrderAsync(string portfolioOrderId, CancellationToken ct = default);

    // --- Realignment (rebalancing) ---
    Task<string> TriggerRealignmentAsync(string allocationId, CancellationToken ct = default);
    Task<bool> RealignmentExistsAsync(string executionId, CancellationToken ct = default);
    Task<IReadOnlyList<UpvestRebalancingOrder>> ListRealignmentOrdersAsync(string executionId, CancellationToken ct = default);

    // --- Portfolio valuation ---
    Task<UpvestValuation?> GetCurrentValuationAsync(string accountId, CancellationToken ct = default);
}
