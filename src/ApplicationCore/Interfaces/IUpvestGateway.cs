using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The shop's gateway to the Upvest Investment API. Each method maps to one (or a focused set of)
/// Upvest endpoint(s). Implementations handle authentication, request signing and (de)serialisation.
/// All personal data passes through but is never logged.
/// </summary>
public interface IUpvestGateway
{
    // --- Investor onboarding (users / checks / tax / accounts) ---
    Task<UpvestUser> CreateUserAsync(InvestorSignUp signUp, CancellationToken ct = default);
    Task<UpvestUser> GetUserAsync(Guid userId, CancellationToken ct = default);
    Task SetTaxResidenciesAsync(Guid userId, string taxCountry, string taxIdentifier, CancellationToken ct = default);
    Task CreateIdentifierAsync(Guid userId, IdentityDocument document, CancellationToken ct = default);
    Task CreateKycCheckAsync(Guid userId, IdentityDocument document, string nationality, CancellationToken ct = default);
    Task<UpvestAccountGroup> CreateAccountGroupAsync(Guid userId, CancellationToken ct = default);
    Task<UpvestAccount> CreateAccountAsync(Guid userId, Guid accountGroupId, CancellationToken ct = default);
    Task<UpvestAccount> GetAccountAsync(Guid accountId, CancellationToken ct = default);

    // --- Model portfolios (allocations / configurations) ---
    Task<UpvestAllocation> CreateAllocationAsync(string name, IEnumerable<AllocationFund> funds, CancellationToken ct = default);
    Task<UpvestAllocation> UpdateAllocationAsync(Guid allocationId, string name, IEnumerable<AllocationFund> funds, CancellationToken ct = default);
    Task CreateConfigurationAsync(Guid accountId, Guid allocationId, CancellationToken ct = default);
    Task UpdateConfigurationAsync(Guid accountId, Guid allocationId, CancellationToken ct = default);

    // --- Paying in (credit cash + invest) ---
    Task<UpvestVirtualCashIncrease> IncreaseVirtualCashAsync(Guid accountGroupId, decimal amount, string currency, CancellationToken ct = default);
    Task<UpvestPortfolioOrder> CreatePortfolioOrderAsync(Guid userId, Guid accountId, decimal amount, string currency, string side, CancellationToken ct = default);
    Task<UpvestPortfolioOrder> GetPortfolioOrderAsync(Guid orderId, CancellationToken ct = default);

    // --- Realignment (rebalancing) ---
    Task<Guid> TriggerRebalancingAsync(Guid allocationId, CancellationToken ct = default);
    Task<UpvestRebalancingExecution> GetRebalancingExecutionAsync(Guid executionId, CancellationToken ct = default);

    // --- Portfolio holdings & value ---
    Task<UpvestValuation> GetValuationAsync(Guid accountId, CancellationToken ct = default);
}
