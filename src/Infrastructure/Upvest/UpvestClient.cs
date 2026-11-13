using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.Infrastructure.Upvest.Models;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Default <see cref="IUpvestClient"/>. Serialises with snake_case JSON, obtains a scoped token
/// per call, signs via the request executor, and refreshes the token once on a 401.
/// </summary>
public class UpvestClient : IUpvestClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly UpvestRequestExecutor _executor;
    private readonly UpvestTokenProvider _tokens;
    private readonly ILogger<UpvestClient> _logger;

    public UpvestClient(UpvestRequestExecutor executor, UpvestTokenProvider tokens, ILogger<UpvestClient> logger)
    {
        _executor = executor;
        _tokens = tokens;
        _logger = logger;
    }

    // ---- Users ----

    public Task<UpvestApiResult<UserResponse>> CreateUserAsync(CreateUserRequest request, CancellationToken ct) =>
        SendAsync<UserResponse>(HttpMethod.Post, "/users", UpvestScopes.UsersAdmin, request, idempotent: true, ct);

    public Task<UpvestApiResult<UserResponse>> GetUserAsync(string userId, CancellationToken ct) =>
        SendAsync<UserResponse>(HttpMethod.Get, $"/users/{userId}", UpvestScopes.UsersRead, null, idempotent: false, ct);

    public Task<UpvestApiResult<CheckResponse>> SubmitCheckAsync(string userId, CreateCheckRequest request, CancellationToken ct) =>
        SendAsync<CheckResponse>(HttpMethod.Post, $"/users/{userId}/checks", UpvestScopes.Checks, request, idempotent: true, ct);

    // ---- Account groups & accounts ----

    public Task<UpvestApiResult<AccountGroupResponse>> CreateAccountGroupAsync(CreateAccountGroupRequest request, CancellationToken ct) =>
        SendAsync<AccountGroupResponse>(HttpMethod.Post, "/account_groups", UpvestScopes.Accounts, request, idempotent: true, ct);

    public Task<UpvestApiResult<AccountGroupResponse>> GetAccountGroupAsync(string accountGroupId, CancellationToken ct) =>
        SendAsync<AccountGroupResponse>(HttpMethod.Get, $"/account_groups/{accountGroupId}", UpvestScopes.Accounts, null, idempotent: false, ct);

    public Task<UpvestApiResult<AccountResponse>> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct) =>
        SendAsync<AccountResponse>(HttpMethod.Post, "/accounts", UpvestScopes.Accounts, request, idempotent: true, ct);

    public Task<UpvestApiResult<AccountResponse>> GetAccountAsync(string accountId, CancellationToken ct) =>
        SendAsync<AccountResponse>(HttpMethod.Get, $"/accounts/{accountId}", UpvestScopes.Accounts, null, idempotent: false, ct);

    // ---- Portfolio allocations & configurations ----

    public Task<UpvestApiResult<AllocationResponse>> CreateAllocationAsync(CreateAllocationRequest request, CancellationToken ct) =>
        SendAsync<AllocationResponse>(HttpMethod.Post, "/portfolios/allocations", UpvestScopes.PortfoliosAdmin, request, idempotent: true, ct);

    public Task<UpvestApiResult<AllocationResponse>> ReplaceAllocationAsync(string allocationId, CreateAllocationRequest request, CancellationToken ct) =>
        SendAsync<AllocationResponse>(HttpMethod.Put, $"/portfolios/allocations/{allocationId}", UpvestScopes.PortfoliosAdmin, request, idempotent: true, ct);

    public Task<UpvestApiResult<ConfigurationResponse>> CreateConfigurationAsync(CreateConfigurationRequest request, CancellationToken ct) =>
        SendAsync<ConfigurationResponse>(HttpMethod.Post, "/portfolios/configurations", UpvestScopes.PortfoliosAdmin, request, idempotent: true, ct);

    // ---- Funding & orders ----

    public Task<UpvestApiResult<TopupResponse>> CreateTopupAsync(CreateTopupRequest request, CancellationToken ct) =>
        SendAsync<TopupResponse>(HttpMethod.Post, "/payments/topups", UpvestScopes.Payments, request, idempotent: true, ct);

    public Task<UpvestApiResult<PortfolioOrderResponse>> CreatePortfolioOrderAsync(CreatePortfolioOrderRequest request, CancellationToken ct) =>
        SendAsync<PortfolioOrderResponse>(HttpMethod.Post, "/portfolios/orders", UpvestScopes.PortfoliosAdmin, request, idempotent: true, ct);

    public Task<UpvestApiResult<PortfolioOrderResponse>> GetPortfolioOrderAsync(string orderId, CancellationToken ct) =>
        SendAsync<PortfolioOrderResponse>(HttpMethod.Get, $"/portfolios/orders/{orderId}", UpvestScopes.PortfoliosRead, null, idempotent: false, ct);

    // ---- Rebalancing ----

    public Task<UpvestApiResult<RebalancingResponse>> CreateRebalancingAsync(CreateRebalancingRequest request, CancellationToken ct) =>
        SendAsync<RebalancingResponse>(HttpMethod.Post, "/portfolios/rebalancings/executions", UpvestScopes.PortfoliosAdmin, request, idempotent: true, ct);

    public Task<UpvestApiResult<RebalancingResponse>> GetRebalancingAsync(string executionId, CancellationToken ct) =>
        SendAsync<RebalancingResponse>(HttpMethod.Get, $"/portfolios/rebalancings/executions/{executionId}", UpvestScopes.PortfoliosRead, null, idempotent: false, ct);

    public Task<UpvestApiResult<RebalancingOrdersResponse>> GetRebalancingOrdersAsync(string executionId, CancellationToken ct) =>
        SendAsync<RebalancingOrdersResponse>(HttpMethod.Get, $"/portfolios/rebalancings/executions/{executionId}/orders", UpvestScopes.PortfoliosRead, null, idempotent: false, ct);

    // ---- Valuations ----

    public Task<UpvestApiResult<ValuationResponse>> GetCurrentValuationAsync(string accountId, CancellationToken ct) =>
        SendAsync<ValuationResponse>(HttpMethod.Get, $"/accounts/{accountId}/valuations/current", UpvestScopes.Valuations, null, idempotent: false, ct);

    // ---- Core ----

    private async Task<UpvestApiResult<T>> SendAsync<T>(
        HttpMethod method, string path, string scope, object? body, bool idempotent, CancellationToken ct)
    {
        byte[]? payload = null;
        string? contentType = null;
        if (body is not null)
        {
            payload = JsonSerializer.SerializeToUtf8Bytes(body, body.GetType(), Json);
            contentType = "application/json";
        }

        string token;
        try
        {
            token = await _tokens.GetTokenAsync(scope, ct);
        }
        catch (UpvestApiException ex)
        {
            // The client is not authorised for these scopes, or the token endpoint refused us.
            return UpvestApiResult<T>.Failure(ex.StatusCode, ex.Message);
        }

        var idempotencyKey = idempotent ? Guid.NewGuid().ToString() : null;

        UpvestRawResponse response;
        try
        {
            response = await _executor.SendAsync(method, path, null, payload, contentType, token, idempotencyKey, ct);

            if (response.StatusCode == 401)
            {
                // Token may have expired mid-use; refresh once and retry.
                _tokens.Invalidate(scope);
                token = await _tokens.GetTokenAsync(scope, ct, forceRefresh: true);
                response = await _executor.SendAsync(method, path, null, payload, contentType, token, idempotencyKey, ct);
            }
        }
        catch (UpvestApiException ex)
        {
            return UpvestApiResult<T>.Failure(ex.StatusCode, ex.Message);
        }

        if (!response.IsSuccess)
        {
            return UpvestApiResult<T>.Failure(response.StatusCode, UpvestProblem.Summarize(response.Body, response.StatusCode));
        }

        if (string.IsNullOrWhiteSpace(response.Body))
        {
            return UpvestApiResult<T>.Success(default!, response.StatusCode);
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(response.Body, Json);
            return UpvestApiResult<T>.Success(value!, response.StatusCode);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Could not parse Upvest {Method} {Path} response: {Message}", method.Method, path, ex.Message);
            return UpvestApiResult<T>.Failure(response.StatusCode, "Unexpected response shape from Upvest.");
        }
    }
}
