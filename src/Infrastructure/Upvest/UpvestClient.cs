using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Typed HTTP client implementing <see cref="IUpvestClient"/>. The underlying
/// <see cref="HttpClient"/> is configured with the Upvest base address and the
/// auth + signing delegating handlers.
/// </summary>
public sealed class UpvestClient : IUpvestClient
{
    public const string HttpClientName = "upvest-api";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;

    public UpvestClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<UpvestUser> CreateUserAsync(UpvestCreateUserRequest request, CancellationToken ct = default) =>
        SendAsync<UpvestUser>(HttpMethod.Post, "users", request, ct);

    public Task<UpvestUser> GetUserAsync(string userId, CancellationToken ct = default) =>
        SendAsync<UpvestUser>(HttpMethod.Get, $"users/{Escape(userId)}", null, ct);

    public Task<UpvestCheck> CreateKycCheckAsync(string userId, UpvestCreateKycCheckRequest request, CancellationToken ct = default) =>
        SendAsync<UpvestCheck>(HttpMethod.Post, $"users/{Escape(userId)}/checks", request, ct);

    public async Task<IReadOnlyList<UpvestCheck>> ListChecksAsync(string userId, CancellationToken ct = default)
    {
        var list = await SendAsync<ListEnvelope<UpvestCheck>>(HttpMethod.Get, $"users/{Escape(userId)}/checks", null, ct)
            .ConfigureAwait(false);
        return list.Data;
    }

    public Task SetTaxResidenciesAsync(string userId, UpvestTaxResidenciesRequest request, CancellationToken ct = default) =>
        SendAsync<JsonElement>(HttpMethod.Post, $"users/{Escape(userId)}/tax_residencies", request, ct);

    public Task<UpvestAccountGroup> CreateAccountGroupAsync(UpvestCreateAccountGroupRequest request, CancellationToken ct = default) =>
        SendAsync<UpvestAccountGroup>(HttpMethod.Post, "account_groups", request, ct);

    public Task<UpvestAccount> CreateAccountAsync(UpvestCreateAccountRequest request, CancellationToken ct = default) =>
        SendAsync<UpvestAccount>(HttpMethod.Post, "accounts", request, ct);

    public Task<UpvestAccount> GetAccountAsync(string accountId, CancellationToken ct = default) =>
        SendAsync<UpvestAccount>(HttpMethod.Get, $"accounts/{Escape(accountId)}", null, ct);

    public Task<UpvestAllocation> CreateAllocationAsync(UpvestAllocationRequest request, CancellationToken ct = default) =>
        SendAsync<UpvestAllocation>(HttpMethod.Post, "portfolios/allocations", request, ct);

    public Task<UpvestAllocation> ReplaceAllocationAsync(string allocationId, UpvestAllocationRequest request, CancellationToken ct = default) =>
        SendAsync<UpvestAllocation>(HttpMethod.Put, $"portfolios/allocations/{Escape(allocationId)}", request, ct);

    public Task<UpvestConfiguration> CreateConfigurationAsync(UpvestConfigurationRequest request, CancellationToken ct = default) =>
        SendAsync<UpvestConfiguration>(HttpMethod.Post, "portfolios/configurations", request, ct);

    public Task<UpvestConfiguration> UpdateConfigurationAsync(string accountId, UpvestConfigurationRequest request, CancellationToken ct = default) =>
        SendAsync<UpvestConfiguration>(HttpMethod.Patch, $"portfolios/configurations/{Escape(accountId)}", request, ct);

    public Task<UpvestVirtualCashIncrease> IncreaseVirtualCashAsync(UpvestVirtualCashIncreaseRequest request, CancellationToken ct = default) =>
        SendAsync<UpvestVirtualCashIncrease>(HttpMethod.Post, "virtual_cash_balances/increases", request, ct);

    public Task<UpvestPortfolioOrder> CreatePortfolioOrderAsync(UpvestPortfolioOrderRequest request, CancellationToken ct = default) =>
        SendAsync<UpvestPortfolioOrder>(HttpMethod.Post, "portfolios/orders", request, ct);

    public Task<UpvestPortfolioOrder> GetPortfolioOrderAsync(string orderId, CancellationToken ct = default) =>
        SendAsync<UpvestPortfolioOrder>(HttpMethod.Get, $"portfolios/orders/{Escape(orderId)}", null, ct);

    public Task<UpvestRebalancingExecution> TriggerRebalancingAsync(UpvestRebalancingRequest request, CancellationToken ct = default) =>
        SendAsync<UpvestRebalancingExecution>(HttpMethod.Post, "portfolios/rebalancings/executions", request, ct);

    public async Task<IReadOnlyList<UpvestRebalancingOrder>> GetRebalancingOrdersAsync(string executionId, CancellationToken ct = default)
    {
        var list = await SendAsync<UpvestRebalancingOrderList>(
            HttpMethod.Get, $"portfolios/rebalancings/executions/{Escape(executionId)}/orders", null, ct).ConfigureAwait(false);
        return list.Data;
    }

    public Task<UpvestValuation> GetCurrentValuationAsync(string accountId, CancellationToken ct = default) =>
        SendAsync<UpvestValuation>(HttpMethod.Get, $"accounts/{Escape(accountId)}/valuations/current?price_quality=HIGHEST_AVAILABLE", null, ct);

    private async Task<TResponse> SendAsync<TResponse>(HttpMethod method, string relativeUrl, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, relativeUrl);
        if (body != null)
        {
            var json = JsonSerializer.Serialize(body, body.GetType(), JsonOptions);
            // Upvest rejects a charset parameter on the Content-Type, so set it explicitly.
            request.Content = new StringContent(json, System.Text.Encoding.UTF8);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        }

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw await BuildExceptionAsync(response, ct).ConfigureAwait(false);

        if (typeof(TResponse) == typeof(JsonElement) && response.Content.Headers.ContentLength is 0 or null)
            return default!;

        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(payload))
            return default!;

        return JsonSerializer.Deserialize<TResponse>(payload, JsonOptions)!;
    }

    private static async Task<UpvestApiException> BuildExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        // Surface the Upvest problem+json "title"/"detail" when present, but never the full body
        // (requests/responses can carry personal data).
        string detail = response.ReasonPhrase ?? "Upvest request failed";
        try
        {
            var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(payload))
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                        detail = title.GetString()!;
                    if (root.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                        detail = d.GetString()!;
                }
            }
        }
        catch
        {
            // Ignore parsing problems – fall back to the reason phrase.
        }

        return new UpvestApiException(response.StatusCode, $"Upvest API error (HTTP {(int)response.StatusCode}): {detail}");
    }

    private static string Escape(string segment) => Uri.EscapeDataString(segment);

    private sealed class ListEnvelope<T>
    {
        public List<T> Data { get; set; } = new();
    }
}
