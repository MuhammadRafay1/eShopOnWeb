using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Thin, typed port over the Upvest Investment API. Each method maps to a documented Upvest
/// endpoint. Non-success responses raise <see cref="UpvestApiException"/>.
/// </summary>
public interface IUpvestClient
{
    Task<UpvestUser> CreateUserAsync(UpvestCreateUserRequest request, CancellationToken ct = default);
    Task<UpvestUser> GetUserAsync(string userId, CancellationToken ct = default);

    Task<UpvestCheck> CreateKycCheckAsync(string userId, UpvestCreateKycCheckRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<UpvestCheck>> ListChecksAsync(string userId, CancellationToken ct = default);

    Task SetTaxResidenciesAsync(string userId, UpvestTaxResidenciesRequest request, CancellationToken ct = default);

    Task<UpvestAccountGroup> CreateAccountGroupAsync(UpvestCreateAccountGroupRequest request, CancellationToken ct = default);
    Task<UpvestAccount> CreateAccountAsync(UpvestCreateAccountRequest request, CancellationToken ct = default);
    Task<UpvestAccount> GetAccountAsync(string accountId, CancellationToken ct = default);

    Task<UpvestAllocation> CreateAllocationAsync(UpvestAllocationRequest request, CancellationToken ct = default);
    Task<UpvestAllocation> ReplaceAllocationAsync(string allocationId, UpvestAllocationRequest request, CancellationToken ct = default);

    Task<UpvestConfiguration> CreateConfigurationAsync(UpvestConfigurationRequest request, CancellationToken ct = default);
    Task<UpvestConfiguration> UpdateConfigurationAsync(string accountId, UpvestConfigurationRequest request, CancellationToken ct = default);

    Task<UpvestVirtualCashIncrease> IncreaseVirtualCashAsync(UpvestVirtualCashIncreaseRequest request, CancellationToken ct = default);

    Task<UpvestPortfolioOrder> CreatePortfolioOrderAsync(UpvestPortfolioOrderRequest request, CancellationToken ct = default);
    Task<UpvestPortfolioOrder> GetPortfolioOrderAsync(string orderId, CancellationToken ct = default);

    Task<UpvestRebalancingExecution> TriggerRebalancingAsync(UpvestRebalancingRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<UpvestRebalancingOrder>> GetRebalancingOrdersAsync(string executionId, CancellationToken ct = default);

    Task<UpvestValuation> GetCurrentValuationAsync(string accountId, CancellationToken ct = default);
}
