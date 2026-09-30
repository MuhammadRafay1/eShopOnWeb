using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.Infrastructure.PayPal.Models;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Thin, hand-written HTTP client shaped directly to the operations declared in api-specs/paypal
/// (Checkout Orders v2, Payments v2, Vault Payment Tokens v3, Transaction Search v1). No PayPal
/// SDK is used; every call below corresponds to exactly one spec operation.
/// Never logs request/response bodies (card data may be present in requests).
/// </summary>
public class PayPalApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;

    public PayPalApiClient(HttpClient httpClient, IOptions<PayPalOptions> options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new System.Uri(options.Value.ResolveBaseUrl());
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public Task<OrderResponse> CreateOrderAsync(OrderRequest request, string requestId, CancellationToken ct = default)
        => SendAsync<OrderResponse>(HttpMethod.Post, "/v2/checkout/orders", request, requestId, ct);

    public Task<OrderResponse> AuthorizeOrderAsync(string orderId, string requestId, CancellationToken ct = default)
        => SendAsync<OrderResponse>(HttpMethod.Post, $"/v2/checkout/orders/{orderId}/authorize", new { }, requestId, ct);

    public Task<OrderResponse> GetOrderAsync(string orderId, CancellationToken ct = default)
        => SendAsync<OrderResponse>(HttpMethod.Get, $"/v2/checkout/orders/{orderId}", null, null, ct);

    public Task<AuthorizationResponse> GetAuthorizationAsync(string authorizationId, CancellationToken ct = default)
        => SendAsync<AuthorizationResponse>(HttpMethod.Get, $"/v2/payments/authorizations/{authorizationId}", null, null, ct);

    public Task<CaptureResponse> CaptureAuthorizationAsync(string authorizationId, CaptureRequest request, string requestId, CancellationToken ct = default)
        => SendAsync<CaptureResponse>(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/capture", request, requestId, ct);

    public Task<AuthorizationResponse> ReauthorizeAsync(string authorizationId, ReauthorizeRequest request, string requestId, CancellationToken ct = default)
        => SendAsync<AuthorizationResponse>(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/reauthorize", request, requestId, ct);

    public async Task VoidAuthorizationAsync(string authorizationId, string requestId, CancellationToken ct = default)
    {
        using var response = await SendRawAsync(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/void", null, requestId, ct);
    }

    public Task<RefundResponse> RefundCaptureAsync(string captureId, RefundRequest request, string requestId, CancellationToken ct = default)
        => SendAsync<RefundResponse>(HttpMethod.Post, $"/v2/payments/captures/{captureId}/refund", request, requestId, ct);

    public Task<RefundResponse> GetRefundAsync(string refundId, CancellationToken ct = default)
        => SendAsync<RefundResponse>(HttpMethod.Get, $"/v2/payments/refunds/{refundId}", null, null, ct);

    public Task<PaymentTokenResponse> CreatePaymentTokenAsync(PaymentTokenRequest request, string requestId, CancellationToken ct = default)
        => SendAsync<PaymentTokenResponse>(HttpMethod.Post, "/v3/vault/payment-tokens", request, requestId, ct);

    public async Task DeletePaymentTokenAsync(string tokenId, CancellationToken ct = default)
    {
        using var response = await SendRawAsync(HttpMethod.Delete, $"/v3/vault/payment-tokens/{tokenId}", null, null, ct);
    }

    public Task<SearchResponse> SearchTransactionsAsync(string startDate, string endDate, int page, int pageSize, CancellationToken ct = default)
    {
        var query = $"/v1/reporting/transactions?start_date={System.Uri.EscapeDataString(startDate)}&end_date={System.Uri.EscapeDataString(endDate)}" +
                    $"&fields=transaction_info&balance_affecting_records_only=N&page={page}&page_size={pageSize}";
        return SendAsync<SearchResponse>(HttpMethod.Get, query, null, null, ct);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body, string? requestId, CancellationToken ct)
    {
        using var response = await SendRawAsync(method, url, body, requestId, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(responseBody, JsonOptions)!;
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string url, object? body, string? requestId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        if (!string.IsNullOrEmpty(requestId))
        {
            request.Headers.Add("PayPal-Request-Id", requestId);
        }
        if (method == HttpMethod.Post)
        {
            request.Headers.Add("Prefer", "return=representation");
        }

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            PayPalError? error = null;
            try
            {
                error = JsonSerializer.Deserialize<PayPalError>(errorBody, JsonOptions);
            }
            catch (JsonException)
            {
                // fall through with a generic message below
            }

            var issues = error?.Details?.ConvertAll(d => d.Issue ?? string.Empty) ?? new System.Collections.Generic.List<string>();
            throw new PayPalGatewayException(
                response.StatusCode,
                error?.Name,
                error?.Message ?? $"PayPal call to {url} failed with status {(int)response.StatusCode}.",
                error?.DebugId,
                issues);
        }

        return response;
    }
}
