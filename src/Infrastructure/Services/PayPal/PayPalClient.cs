using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Services.PayPal;

/// <summary>
/// The sole component in this application that talks to PayPal. Handles OAuth2 client-credentials
/// token caching (with one 401-triggered refresh+retry), the base-URL resolution rules from
/// PayPalSettings, idempotency headers (PayPal-Request-Id) on every money-moving call, and a small
/// retry-with-backoff for 429/5xx on idempotent operations. Never logs card data, tokens or secrets.
/// </summary>
public class PayPalClient : IPayPalClient
{
    private const string Intent = "AUTHORIZE";
    private const int TransactionSearchPageSize = 100;
    private const int MaxTransactionSearchWindowDays = 31;

    private readonly HttpClient _httpClient;
    private readonly PayPalSettings _settings;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PayPalClient> _logger;
    private readonly string _tokenCacheKey;

    public PayPalClient(HttpClient httpClient, PayPalSettings settings, IMemoryCache cache, ILogger<PayPalClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _cache = cache;
        _logger = logger;
        _tokenCacheKey = $"PayPalClient:AccessToken:{settings.ClientId}";

        _httpClient.BaseAddress = new Uri(settings.ResolveBaseUrl().TrimEnd('/') + "/");
    }

    public async Task<AuthorizationResult> AuthorizeOrderWithCardAsync(decimal amount, string currency, string customId, string invoiceId, CardDetails card, string requestId)
    {
        var cardRequest = new PayPalCardRequest(card.Name, card.Number, card.Expiry, card.SecurityCode, MapAddress(card.BillingAddress), null);
        return await AuthorizeAsync(amount, currency, customId, invoiceId, cardRequest, requestId);
    }

    public async Task<AuthorizationResult> AuthorizeOrderWithVaultAsync(decimal amount, string currency, string customId, string invoiceId, string vaultId, string requestId)
    {
        var cardRequest = new PayPalCardRequest(null, null, null, null, null, vaultId);
        return await AuthorizeAsync(amount, currency, customId, invoiceId, cardRequest, requestId);
    }

    private async Task<AuthorizationResult> AuthorizeAsync(decimal amount, string currency, string customId, string invoiceId, PayPalCardRequest cardRequest, string requestId)
    {
        var body = new PayPalCreateOrderRequest(
            Intent,
            new List<PayPalPurchaseUnitRequest>
            {
                new(new PayPalMoney(currency, FormatAmount(amount)), customId, invoiceId)
            },
            new PayPalPaymentSourceRequest(cardRequest));

        var response = await SendJsonAsync<PayPalOrderResponse>(HttpMethod.Post, "v2/checkout/orders", body, requestId);

        var authorization = response.PurchaseUnits?.FirstOrDefault()?.Payments?.Authorizations?.FirstOrDefault();
        var cardResponse = response.PaymentSource?.Card;

        var (requiresChallenge, reason) = DetectChallenge(response, authorization, cardResponse);

        return new AuthorizationResult(
            response.Id,
            authorization?.Id ?? string.Empty,
            authorization?.Status ?? response.Status,
            authorization?.ExpirationTime,
            cardResponse?.Brand,
            cardResponse?.LastDigits,
            requiresChallenge,
            reason);
    }

    private static (bool RequiresChallenge, string? Reason) DetectChallenge(PayPalOrderResponse response, PayPalAuthorization? authorization, PayPalCardResponse? card)
    {
        if (authorization == null)
        {
            return (true, $"PayPal order status was '{response.Status}' without a completed card authorization.");
        }

        if (string.Equals(response.Status, "PAYER_ACTION_REQUIRED", StringComparison.OrdinalIgnoreCase))
        {
            return (true, "PayPal order requires payer action (PAYER_ACTION_REQUIRED).");
        }

        if (response.Links?.Any(l => l.Rel is "payer-action" or "approve") == true)
        {
            return (true, "PayPal returned an approval/payer-action link, indicating a buyer approval step is required.");
        }

        var threeDs = card?.AuthenticationResult?.ThreeDSecure;
        if (threeDs?.EnrollmentStatus == "Y" && threeDs.AuthenticationStatus != null && threeDs.AuthenticationStatus != "Y")
        {
            return (true, $"3-D Secure authentication was not completed (status: {threeDs.AuthenticationStatus}).");
        }

        return (false, null);
    }

    public async Task<CaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency, string requestId)
    {
        var body = new PayPalCaptureRequest(new PayPalMoney(currency, FormatAmount(amount)), true);
        var response = await SendJsonAsync<PayPalCaptureResponse>(HttpMethod.Post, $"v2/payments/authorizations/{authorizationId}/capture", body, requestId);

        var breakdown = response.SellerReceivableBreakdown;
        var gross = ParseAmount(breakdown?.GrossAmount ?? response.Amount);
        var fee = ParseAmountNullable(breakdown?.PayPalFee);
        var net = ParseAmountNullable(breakdown?.NetAmount);

        return new CaptureResult(response.Id, response.Status, gross, fee, net);
    }

    public async Task<ReauthorizeResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string requestId)
    {
        var body = new PayPalReauthorizeRequest(new PayPalMoney(currency, FormatAmount(amount)));
        var response = await SendJsonAsync<PayPalReauthorizeResponse>(HttpMethod.Post, $"v2/payments/authorizations/{authorizationId}/reauthorize", body, requestId);

        return new ReauthorizeResult(response.Id, response.Status, response.ExpirationTime);
    }

    public async Task VoidAsync(string authorizationId, string requestId)
    {
        await SendNoContentAsync(HttpMethod.Post, $"v2/payments/authorizations/{authorizationId}/void", null, requestId);
    }

    public async Task<RefundResult> RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey)
    {
        var body = new PayPalRefundRequest(amount.HasValue ? new PayPalMoney(currency, FormatAmount(amount.Value)) : null);
        var response = await SendJsonAsync<PayPalRefundResponse>(HttpMethod.Post, $"v2/payments/captures/{captureId}/refund", body, idempotencyKey);

        var refundedAmount = ParseAmount(response.Amount);
        var totalRefunded = ParseAmountNullable(response.SellerPayableBreakdown?.TotalRefundedAmount) ?? refundedAmount;

        return new RefundResult(response.Id, response.Status, refundedAmount, totalRefunded);
    }

    public async Task<AuthorizationStatusResult> GetAuthorizationAsync(string authorizationId)
    {
        var response = await SendJsonAsync<PayPalAuthorizationDetailsResponse>(HttpMethod.Get, $"v2/payments/authorizations/{authorizationId}", null, null, preferRepresentation: false);
        return new AuthorizationStatusResult(response.Status, response.ExpirationTime);
    }

    public async Task<VaultResult> VaultCardAsync(CardDetails card, string? customerId, string requestId)
    {
        var cardRequest = new PayPalCardRequest(card.Name, card.Number, card.Expiry, card.SecurityCode, MapAddress(card.BillingAddress), null);
        var body = new PayPalVaultCreateRequest(
            new PayPalPaymentSourceRequest(cardRequest),
            customerId != null ? new PayPalVaultCustomerRequest(customerId) : null);

        var response = await SendJsonAsync<PayPalVaultCreateResponse>(HttpMethod.Post, "v3/vault/payment-tokens", body, requestId);
        var responseCard = response.PaymentSource?.Card;

        return new VaultResult(response.Id, response.Customer?.Id, responseCard?.Brand, responseCard?.LastDigits, responseCard?.Expiry);
    }

    public async Task DeleteVaultTokenAsync(string vaultTokenId)
    {
        await SendNoContentAsync(HttpMethod.Delete, $"v3/vault/payment-tokens/{vaultTokenId}", null, null, treatNotFoundAsSuccess: true);
    }

    public async Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, string currency)
    {
        var results = new List<PayPalTransaction>();

        foreach (var (windowStart, windowEnd) in SplitIntoWindows(fromUtc, toUtc))
        {
            var page = 1;
            while (true)
            {
                var path = "v1/reporting/transactions" +
                    $"?start_date={Uri.EscapeDataString(FormatDate(windowStart))}" +
                    $"&end_date={Uri.EscapeDataString(FormatDate(windowEnd))}" +
                    $"&fields=all&transaction_currency={Uri.EscapeDataString(currency)}" +
                    $"&page_size={TransactionSearchPageSize}&page={page}";

                var response = await SendJsonAsync<PayPalTransactionSearchResponse>(HttpMethod.Get, path, null, null, preferRepresentation: false);

                var details = response.TransactionDetails ?? new List<PayPalTransactionDetail>();
                foreach (var detail in details)
                {
                    var info = detail.TransactionInfo;
                    if (info == null)
                    {
                        continue;
                    }

                    results.Add(new PayPalTransaction(
                        info.TransactionId ?? string.Empty,
                        info.TransactionEventCode,
                        info.TransactionStatus,
                        ParseAmount(info.TransactionAmount),
                        info.TransactionAmount?.CurrencyCode ?? currency,
                        ParseAmountNullable(info.FeeAmount),
                        info.InvoiceId,
                        info.CustomField,
                        info.TransactionInitiationDate));
                }

                if (details.Count < TransactionSearchPageSize || (response.TotalPages.HasValue && page >= response.TotalPages.Value))
                {
                    break;
                }

                page++;
            }
        }

        return results;
    }

    private static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> SplitIntoWindows(DateTimeOffset from, DateTimeOffset to)
    {
        var cursor = from;
        while (cursor < to)
        {
            var windowEnd = cursor.AddDays(MaxTransactionSearchWindowDays);
            if (windowEnd > to)
            {
                windowEnd = to;
            }
            yield return (cursor, windowEnd);
            cursor = windowEnd;
        }
    }

    private static string FormatDate(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string FormatAmount(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static decimal ParseAmount(PayPalMoney? money) =>
        money != null && decimal.TryParse(money.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;

    private static decimal? ParseAmountNullable(PayPalMoney? money) => money == null ? null : ParseAmount(money);

    private static decimal ParseAmount(PayPalTransactionAmount? amount) =>
        amount?.Value != null && decimal.TryParse(amount.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;

    private static decimal? ParseAmountNullable(PayPalTransactionAmount? amount) => amount?.Value == null ? null : ParseAmount(amount);

    private static PayPalAddressRequest MapAddress(PayPalBillingAddress address) =>
        new(address.AddressLine1, address.AddressLine2, address.AdminArea2, address.AdminArea1, address.PostalCode, address.CountryCode);

    private async Task<string> GetAccessTokenAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _cache.TryGetValue(_tokenCacheKey, out string? cached) && cached != null)
        {
            return cached;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token");
        var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("PayPal token request failed with status {Status}", (int)response.StatusCode);
            throw new PayPalApiException("Failed to obtain a PayPal access token", payPalStatusCode: (int)response.StatusCode);
        }

        var token = await response.Content.ReadFromJsonAsync<PayPalTokenResponse>()
            ?? throw new PayPalApiException("PayPal token response was empty");

        var ttl = TimeSpan.FromSeconds(Math.Max(token.ExpiresIn - 60, 30));
        _cache.Set(_tokenCacheKey, token.AccessToken, ttl);

        return token.AccessToken;
    }

    private async Task<HttpResponseMessage> ExecuteAsync(HttpMethod method, string path, object? body, string? requestId, bool preferRepresentation)
    {
        var token = await GetAccessTokenAsync();
        var response = await SendOnceAsync(method, path, body, requestId, preferRepresentation, token);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            token = await GetAccessTokenAsync(forceRefresh: true);
            response = await SendOnceAsync(method, path, body, requestId, preferRepresentation, token);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string path, object? body, string? requestId, bool preferRepresentation, string token)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (requestId != null)
        {
            request.Headers.Add("PayPal-Request-Id", requestId);
        }

        if (preferRepresentation)
        {
            request.Headers.Add("Prefer", "return=representation");
        }

        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _httpClient.SendAsync(request);
    }

    private async Task<T> SendJsonAsync<T>(HttpMethod method, string path, object? body, string? requestId, bool preferRepresentation = true)
    {
        const int maxAttempts = 4;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var response = await ExecuteAsync(method, path, body, requestId, preferRepresentation);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<T>();
                return result ?? throw new PayPalApiException($"PayPal returned an empty response for {method} {path}");
            }

            var status = (int)response.StatusCode;
            var error = await TryReadErrorAsync(response);
            var detail = error?.Details?.FirstOrDefault();

            _logger.LogWarning("PayPal {Method} {Path} failed with {Status} {Name} issue={Issue} debug_id={DebugId}", method, path, status, error?.Name, detail?.Issue, error?.DebugId);

            if (attempt < maxAttempts && (status == 429 || status >= 500))
            {
                await Task.Delay(BackoffDelay(attempt));
                continue;
            }

            throw new PayPalApiException(
                BuildErrorMessage(path, status, error, detail),
                error?.Name, error?.DebugId, status, status == 429 || status >= 500);
        }

        throw new PayPalApiException($"PayPal request to {path} failed after {maxAttempts} attempts");
    }

    private static string BuildErrorMessage(string path, int status, PayPalErrorResponse? error, PayPalErrorDetail? detail)
    {
        var message = error?.Message ?? $"PayPal request to {path} failed with status {status}";
        if (detail != null)
        {
            message += $" [{detail.Issue}] {detail.Description}";
        }
        return message;
    }

    private async Task SendNoContentAsync(HttpMethod method, string path, object? body, string? requestId, bool treatNotFoundAsSuccess = false, bool preferRepresentation = false)
    {
        const int maxAttempts = 4;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var response = await ExecuteAsync(method, path, body, requestId, preferRepresentation);

            if (response.IsSuccessStatusCode || (treatNotFoundAsSuccess && response.StatusCode == HttpStatusCode.NotFound))
            {
                return;
            }

            var status = (int)response.StatusCode;
            var error = await TryReadErrorAsync(response);
            var detail = error?.Details?.FirstOrDefault();

            _logger.LogWarning("PayPal {Method} {Path} failed with {Status} {Name} issue={Issue} debug_id={DebugId}", method, path, status, error?.Name, detail?.Issue, error?.DebugId);

            if (attempt < maxAttempts && (status == 429 || status >= 500))
            {
                await Task.Delay(BackoffDelay(attempt));
                continue;
            }

            throw new PayPalApiException(
                BuildErrorMessage(path, status, error, detail),
                error?.Name, error?.DebugId, status, status == 429 || status >= 500);
        }

        throw new PayPalApiException($"PayPal request to {path} failed after {maxAttempts} attempts");
    }

    private static TimeSpan BackoffDelay(int attempt) => TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 200 + Random.Shared.Next(0, 150));

    private static async Task<PayPalErrorResponse?> TryReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<PayPalErrorResponse>();
        }
        catch
        {
            return null;
        }
    }
}
