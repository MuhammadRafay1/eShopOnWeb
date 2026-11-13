using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.Infrastructure.Upvest.Models;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// A thin, typed client over the Upvest Investment API. Each method performs one API call,
/// obtaining a correctly-scoped access token and signing the request. Results are returned as
/// <see cref="UpvestApiResult{T}"/> so callers can react to Upvest's actual outcome.
/// </summary>
public interface IUpvestClient
{
    Task<UpvestApiResult<UserResponse>> CreateUserAsync(CreateUserRequest request, CancellationToken ct);
    Task<UpvestApiResult<UserResponse>> GetUserAsync(string userId, CancellationToken ct);
    Task<UpvestApiResult<CheckResponse>> SubmitCheckAsync(string userId, CreateCheckRequest request, CancellationToken ct);

    Task<UpvestApiResult<AccountGroupResponse>> CreateAccountGroupAsync(CreateAccountGroupRequest request, CancellationToken ct);
    Task<UpvestApiResult<AccountGroupResponse>> GetAccountGroupAsync(string accountGroupId, CancellationToken ct);
    Task<UpvestApiResult<AccountResponse>> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct);
    Task<UpvestApiResult<AccountResponse>> GetAccountAsync(string accountId, CancellationToken ct);

    Task<UpvestApiResult<AllocationResponse>> CreateAllocationAsync(CreateAllocationRequest request, CancellationToken ct);
    Task<UpvestApiResult<AllocationResponse>> ReplaceAllocationAsync(string allocationId, CreateAllocationRequest request, CancellationToken ct);
    Task<UpvestApiResult<ConfigurationResponse>> CreateConfigurationAsync(CreateConfigurationRequest request, CancellationToken ct);

    Task<UpvestApiResult<TopupResponse>> CreateTopupAsync(CreateTopupRequest request, CancellationToken ct);
    Task<UpvestApiResult<PortfolioOrderResponse>> CreatePortfolioOrderAsync(CreatePortfolioOrderRequest request, CancellationToken ct);
    Task<UpvestApiResult<PortfolioOrderResponse>> GetPortfolioOrderAsync(string orderId, CancellationToken ct);

    Task<UpvestApiResult<RebalancingResponse>> CreateRebalancingAsync(CreateRebalancingRequest request, CancellationToken ct);
    Task<UpvestApiResult<RebalancingResponse>> GetRebalancingAsync(string executionId, CancellationToken ct);
    Task<UpvestApiResult<RebalancingOrdersResponse>> GetRebalancingOrdersAsync(string executionId, CancellationToken ct);

    Task<UpvestApiResult<ValuationResponse>> GetCurrentValuationAsync(string accountId, CancellationToken ct);
}
