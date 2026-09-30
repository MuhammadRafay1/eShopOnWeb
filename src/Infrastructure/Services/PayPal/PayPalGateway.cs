using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.PayPal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Services.PayPal;

/// <summary>
/// Plain-HTTP adapter over PayPal's REST APIs (OAuth, Orders v2, Payments v2, Vault v3, Transaction
/// Search v1). Never logs request/response bodies - several of these calls carry raw card data, and the
/// simplest way to guarantee it never reaches a log is to not log bodies for any call.
/// </summary>
public class PayPalGateway : IPayPalGateway
{
    private const string TokenCacheKey = "PayPal:AccessToken";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly PayPalSettings _settings;
    private readonly IMemoryCache _cache;
    private readonly IAppLogger<PayPalGateway> _logger;

    public PayPalGateway(HttpClient httpClient, IOptions<PayPalSettings> settings, IMemoryCache cache, IAppLogger<PayPalGateway> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PayPalAuthorizationResult> AuthorizeAsync(PayPalAuthorizeRequest request, CancellationToken ct = default)
    {
        var amountValue = CurrencyFormatter.Format(request.Amount, request.Currency);
        var invoiceId = $"eshop-{request.EshopOrderId}-{Guid.NewGuid():N}";
        if (invoiceId.Length > 127) invoiceId = invoiceId[..127];

        var purchaseUnit = new PurchaseUnitRequestDto(
            ReferenceId: request.EshopOrderId,
            CustomId: request.EshopOrderId,
            InvoiceId: invoiceId,
            Amount: new AmountDto(request.Currency, amountValue, new AmountBreakdownDto(new AmountDto(request.Currency, amountValue))));

        var card = request.VaultId is not null
            ? new CardRequestDto(null, null, null, null, null, request.VaultId, null)
            : BuildCardRequest(request.Card!, includeAttributes: true);

        var body = new CreateOrderRequestDto("AUTHORIZE", new[] { purchaseUnit }, new PaymentSourceRequestDto(card));

        var response = await SendAsync(HttpMethod.Post, "/v2/checkout/orders", body, request.IdempotencyKey, preferRepresentation: true, ct);
        using var doc = await ReadJsonOrThrowAsync(response, "authorize order", ct);
        var root = doc.RootElement;

        var status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
        if (string.Equals(status, "PAYER_ACTION_REQUIRED", StringComparison.OrdinalIgnoreCase) || HasPayerActionLink(root))
        {
            throw new PayPalChallengeRequiredException(
                $"PayPal requires the shopper to complete an additional approval step (3-D Secure / payer action) for this card. " +
                $"This integration only supports payments that complete synchronously without a browser step. PayPal order status: {status ?? "unknown"}.");
        }

        var payPalOrderId = root.GetProperty("id").GetString()!;
        var purchaseUnitResult = root.GetProperty("purchase_units")[0];
        var authorization = purchaseUnitResult.GetProperty("payments").GetProperty("authorizations")[0];
        var authorizationId = authorization.GetProperty("id").GetString()!;
        var authorizationStatus = authorization.GetProperty("status").GetString()!;
        var expiresAt = ParseOptionalDate(authorization, "expiration_time");

        _logger.LogInformation("PayPal authorized order {0} -> PayPal order {1}, authorization {2}, status {3}", request.EshopOrderId, payPalOrderId, authorizationId, authorizationStatus);

        return new PayPalAuthorizationResult(payPalOrderId, authorizationId, authorizationStatus, expiresAt);
    }

    public async Task<PayPalCaptureResult> CaptureAsync(string authorizationId, string idempotencyKey, CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/capture", new CaptureRequestDto(true), idempotencyKey, preferRepresentation: true, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = string.IsNullOrWhiteSpace(body) ? JsonDocument.Parse("{}") : JsonDocument.Parse(body);

        if (!response.IsSuccessStatusCode)
        {
            var issue = ExtractIssue(doc.RootElement);
            _logger.LogWarning("PayPal capture failed for authorization {0}: HTTP {1}, issue {2}", authorizationId, (int)response.StatusCode, issue ?? "(none)");
            throw new PayPalAuthorizationStaleException(
                authorizationId,
                $"PayPal could not capture authorization {authorizationId} (HTTP {(int)response.StatusCode}{(issue is null ? string.Empty : $", issue {issue}")}). The authorization may have expired; a reauthorization will be attempted.",
                issue);
        }

        var root = doc.RootElement;
        var captureId = root.GetProperty("id").GetString()!;
        var status = root.GetProperty("status").GetString()!;
        var breakdown = root.GetProperty("seller_receivable_breakdown");
        var gross = ParseAmount(breakdown.GetProperty("gross_amount"));
        var fee = ParseAmount(breakdown.GetProperty("paypal_fee"));
        var net = ParseAmount(breakdown.GetProperty("net_amount"));

        _logger.LogInformation("PayPal captured authorization {0} -> capture {1}, status {2}", authorizationId, captureId, status);

        return new PayPalCaptureResult(captureId, status, gross, fee, net);
    }

    public async Task<PayPalAuthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string idempotencyKey, CancellationToken ct = default)
    {
        var body = new ReauthorizeRequestDto(new AmountDto(currency, CurrencyFormatter.Format(amount, currency)));
        var response = await SendAsync(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/reauthorize", body, idempotencyKey, preferRepresentation: true, ct);
        using var doc = await ReadJsonOrThrowAsync(response, "reauthorize", ct);
        var root = doc.RootElement;

        var newAuthorizationId = root.GetProperty("id").GetString()!;
        var status = root.GetProperty("status").GetString()!;
        var expiresAt = ParseOptionalDate(root, "expiration_time");

        _logger.LogInformation("PayPal reauthorized {0} -> new authorization {1}, status {2}", authorizationId, newAuthorizationId, status);

        return new PayPalAuthorizationResult(string.Empty, newAuthorizationId, status, expiresAt);
    }

    public async Task VoidAsync(string authorizationId, string idempotencyKey, CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/void", null, idempotencyKey, preferRepresentation: false, ct);
        using var doc = await ReadJsonOrThrowAsync(response, "void authorization", ct);
        _logger.LogInformation("PayPal voided authorization {0}", authorizationId);
    }

    public async Task<PayPalRefundResult> RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey, CancellationToken ct = default)
    {
        object? body = amount.HasValue ? new RefundRequestDto(new AmountDto(currency, CurrencyFormatter.Format(amount.Value, currency))) : null;
        var response = await SendAsync(HttpMethod.Post, $"/v2/payments/captures/{captureId}/refund", body, idempotencyKey, preferRepresentation: true, ct);
        using var doc = await ReadJsonOrThrowAsync(response, "refund capture", ct);
        var root = doc.RootElement;

        var refundId = root.GetProperty("id").GetString()!;
        var status = root.GetProperty("status").GetString()!;
        var refundedAmount = ParseAmount(root.GetProperty("seller_payable_breakdown").GetProperty("total_refunded_amount"));

        _logger.LogInformation("PayPal refunded capture {0} -> refund {1}, amount {2}, status {3}", captureId, refundId, refundedAmount, status);

        return new PayPalRefundResult(refundId, status, refundedAmount);
    }

    public async Task<PayPalVaultResult> VaultCardAsync(PayPalCardDetails card, CancellationToken ct = default)
    {
        var setupBody = new { payment_source = new PaymentSourceRequestDto(BuildCardRequest(card, includeAttributes: false)) };
        var setupResponse = await SendAsync(HttpMethod.Post, "/v3/vault/setup-tokens", setupBody, idempotencyKey: null, preferRepresentation: false, ct);
        using var setupDoc = await ReadJsonOrThrowAsync(setupResponse, "create vault setup token", ct);
        var setupTokenId = setupDoc.RootElement.GetProperty("id").GetString()!;

        var tokenBody = new PaymentTokenRequestDto(new PaymentSourceTokenRequestDto(new TokenRefDto(setupTokenId, "SETUP_TOKEN")));
        var tokenResponse = await SendAsync(HttpMethod.Post, "/v3/vault/payment-tokens", tokenBody, $"vault-{setupTokenId}", preferRepresentation: false, ct);
        using var tokenDoc = await ReadJsonOrThrowAsync(tokenResponse, "create vault payment token", ct);
        var root = tokenDoc.RootElement;

        var vaultId = root.GetProperty("id").GetString()!;
        var customerId = root.GetProperty("customer").GetProperty("id").GetString()!;
        var cardInfo = root.GetProperty("payment_source").GetProperty("card");
        var brand = cardInfo.TryGetProperty("brand", out var brandEl) ? brandEl.GetString() ?? string.Empty : string.Empty;
        var last4 = cardInfo.TryGetProperty("last_digits", out var last4El) ? last4El.GetString() ?? string.Empty : string.Empty;
        var expiry = cardInfo.TryGetProperty("expiry", out var expiryEl) ? expiryEl.GetString() ?? string.Empty : string.Empty;

        _logger.LogInformation("PayPal vaulted a card -> vault id {0}, customer {1}", vaultId, customerId);

        return new PayPalVaultResult(vaultId, customerId, brand, last4, expiry);
    }

    public async Task DeleteVaultedCardAsync(string vaultId, CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Delete, $"/v3/vault/payment-tokens/{vaultId}", null, null, false, ct);
        using var doc = await ReadJsonOrThrowAsync(response, "delete vaulted card", ct);
        _logger.LogInformation("PayPal deleted vaulted card {0}", vaultId);
    }

    public async Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var results = new List<PayPalTransaction>();
        var windowStart = from;

        while (windowStart < to)
        {
            var windowEnd = windowStart.AddDays(31) < to ? windowStart.AddDays(31) : to;
            if (windowEnd <= windowStart) break;

            var page = 1;
            var totalPages = 1;
            do
            {
                var path = "/v1/reporting/transactions" +
                    $"?start_date={Uri.EscapeDataString(windowStart.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture))}" +
                    $"&end_date={Uri.EscapeDataString(windowEnd.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture))}" +
                    "&fields=all&page_size=500&page=" + page;

                var response = await SendAsync(HttpMethod.Get, path, null, null, false, ct);
                using var doc = await ReadJsonOrThrowAsync(response, "search transactions", ct);
                var root = doc.RootElement;

                totalPages = root.TryGetProperty("total_pages", out var tp) ? tp.GetInt32() : 1;

                if (root.TryGetProperty("transaction_details", out var details) && details.ValueKind == JsonValueKind.Array)
                {
                    foreach (var detail in details.EnumerateArray())
                    {
                        results.Add(ParseTransaction(detail));
                    }
                }

                page++;
            } while (page <= totalPages);

            windowStart = windowEnd;
        }

        return results;
    }

    private static PayPalTransaction ParseTransaction(JsonElement detail)
    {
        var info = detail.GetProperty("transaction_info");
        var transactionId = info.GetProperty("transaction_id").GetString()!;
        var status = info.TryGetProperty("transaction_status", out var st) ? st.GetString() ?? string.Empty : string.Empty;

        decimal amount = 0;
        var currency = string.Empty;
        if (info.TryGetProperty("transaction_amount", out var amountEl) && amountEl.ValueKind == JsonValueKind.Object)
        {
            currency = amountEl.GetProperty("currency_code").GetString() ?? string.Empty;
            amount = ParseAmount(amountEl);
        }

        decimal fee = 0;
        if (info.TryGetProperty("fee_amount", out var feeEl) && feeEl.ValueKind == JsonValueKind.Object)
        {
            fee = Math.Abs(ParseAmount(feeEl));
        }

        var invoiceId = info.TryGetProperty("invoice_id", out var invEl) ? invEl.GetString() : null;
        var customField = info.TryGetProperty("custom_field", out var custEl) ? custEl.GetString() : null;
        var transactionDate = info.TryGetProperty("transaction_initiation_date", out var dateEl) &&
                               DateTimeOffset.TryParse(dateEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
            ? parsedDate
            : DateTimeOffset.MinValue;

        return new PayPalTransaction(transactionId, status, amount, currency, fee, invoiceId, customField, transactionDate);
    }

    private static CardRequestDto BuildCardRequest(PayPalCardDetails card, bool includeAttributes)
    {
        var billingAddress = new BillingAddressRequestDto(
            card.BillingAddress.Line1,
            card.BillingAddress.City,
            card.BillingAddress.State,
            card.BillingAddress.PostalCode,
            card.BillingAddress.CountryCode);

        var attributes = includeAttributes ? new CardAttributesRequestDto(new VerificationRequestDto("SCA_WHEN_REQUIRED")) : null;

        return new CardRequestDto(card.Number, card.Expiry, card.SecurityCode, card.Name, billingAddress, null, attributes);
    }

    private static bool HasPayerActionLink(JsonElement root)
    {
        if (!root.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array) return false;
        foreach (var link in links.EnumerateArray())
        {
            if (link.TryGetProperty("rel", out var rel) &&
                (string.Equals(rel.GetString(), "payer-action", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(rel.GetString(), "3ds-contingency", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }

    private static string? ExtractIssue(JsonElement root)
    {
        if (root.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array && details.GetArrayLength() > 0)
        {
            var first = details[0];
            if (first.TryGetProperty("issue", out var issueEl)) return issueEl.GetString();
        }
        return null;
    }

    private static DateTimeOffset? ParseOptionalDate(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var el) && el.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(el.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : null;

    private static decimal ParseAmount(JsonElement amountElement) =>
        decimal.Parse(amountElement.GetProperty("value").GetString()!, CultureInfo.InvariantCulture);

    private async Task<JsonDocument> ReadJsonOrThrowAsync(HttpResponseMessage response, string context, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        var doc = string.IsNullOrWhiteSpace(body) ? JsonDocument.Parse("{}") : JsonDocument.Parse(body);

        if (response.IsSuccessStatusCode) return doc;

        var issue = ExtractIssue(doc.RootElement);
        string message;
        if (issue is not null)
        {
            var description = doc.RootElement.TryGetProperty("details", out var details) && details.GetArrayLength() > 0 &&
                               details[0].TryGetProperty("description", out var descEl)
                ? descEl.GetString()
                : null;
            message = $"PayPal {context} failed: {issue}{(description is null ? string.Empty : $" - {description}")}";
        }
        else if (doc.RootElement.TryGetProperty("message", out var msgEl))
        {
            message = $"PayPal {context} failed: {msgEl.GetString()}";
        }
        else
        {
            message = $"PayPal {context} failed with HTTP {(int)response.StatusCode}.";
        }

        _logger.LogWarning("PayPal {0} failed: HTTP {1}, issue {2}", context, (int)response.StatusCode, issue ?? "(none)");
        doc.Dispose();

        var statusCode = response.StatusCode switch
        {
            HttpStatusCode.UnprocessableEntity => 422,
            HttpStatusCode.Conflict => 409,
            HttpStatusCode.BadRequest => 400,
            HttpStatusCode.NotFound => 404,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => 502,
            _ => 502
        };

        throw new PayPalException(message, statusCode, issue);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? jsonBody, string? idempotencyKey, bool preferRepresentation, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var response = await SendCoreAsync(method, path, jsonBody, idempotencyKey, preferRepresentation, token, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            _cache.Remove(TokenCacheKey);
            token = await GetAccessTokenAsync(ct);
            response = await SendCoreAsync(method, path, jsonBody, idempotencyKey, preferRepresentation, token, ct);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, object? jsonBody, string? idempotencyKey, bool preferRepresentation, string accessToken, CancellationToken ct)
    {
        var baseUrl = ResolveBaseUrl();
        using var request = new HttpRequestMessage(method, baseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (idempotencyKey is not null)
        {
            request.Headers.Add("PayPal-Request-Id", idempotencyKey);
        }

        if (preferRepresentation)
        {
            request.Headers.Add("Prefer", "return=representation");
        }

        if (jsonBody is not null)
        {
            request.Content = JsonContent.Create(jsonBody, jsonBody.GetType(), options: JsonOptions);
        }

        return await _httpClient.SendAsync(request, ct);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(TokenCacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            return cachedToken;
        }

        var baseUrl = ResolveBaseUrl();
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/v1/oauth2/token");
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });

        using var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("PayPal OAuth token request failed: HTTP {0}", (int)response.StatusCode);
            throw new PayPalException($"PayPal authentication failed with HTTP {(int)response.StatusCode}.", 502);
        }

        var token = JsonSerializer.Deserialize<TokenResponse>(body, JsonOptions)!;
        _cache.Set(TokenCacheKey, token.AccessToken, TimeSpan.FromSeconds(Math.Max(60, token.ExpiresIn - 60)));
        return token.AccessToken;
    }

    private string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            return _settings.BaseUrl!.TrimEnd('/');
        }

        var env = _settings.Environment?.Trim().ToLowerInvariant();
        return env is "live" or "production" ? "https://api-m.paypal.com" : "https://api-m.sandbox.paypal.com";
    }
}
