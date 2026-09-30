using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Services;

/// <summary>
/// Typed wrapper over the PayPal REST APIs (OAuth, Orders v2, Payments v2, Vault v3,
/// Transaction Search v1) confirmed live against the PayPal sandbox before this was written.
/// Never logs request/response bodies - only method, path, HTTP status and (on error) PayPal's
/// own error name/debug_id, none of which can contain card data.
/// </summary>
public class PayPalClient : IPayPalClient
{
    private readonly HttpClient _httpClient;
    private readonly PayPalSettings _settings;
    private readonly IAppLogger<PayPalClient> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public PayPalClient(HttpClient httpClient, IOptions<PayPalSettings> settings, IAppLogger<PayPalClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<PayPalAuthorizationResult> AuthorizeOrderWithCardAsync(decimal amount, string currency,
        string customId, string invoiceId, PayPalCardDetails card, string idempotencyKey, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["intent"] = "AUTHORIZE",
            ["purchase_units"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["custom_id"] = customId,
                    ["invoice_id"] = invoiceId,
                    ["amount"] = Money(amount, currency)
                }
            },
            ["payment_source"] = new Dictionary<string, object?>
            {
                ["card"] = new Dictionary<string, object?>
                {
                    ["number"] = card.Number,
                    ["expiry"] = card.ExpiryYearMonth,
                    ["name"] = card.CardholderName,
                    ["billing_address"] = BillingAddress(card),
                    ["attributes"] = new Dictionary<string, object?>
                    {
                        ["verification"] = new Dictionary<string, object?> { ["method"] = "SCA_WHEN_REQUIRED" }
                    }
                }
            }
        };
        return await AuthorizeOrderAsync(body, idempotencyKey, ct);
    }

    public async Task<PayPalAuthorizationResult> AuthorizeOrderWithVaultAsync(decimal amount, string currency,
        string customId, string invoiceId, string vaultId, string idempotencyKey, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["intent"] = "AUTHORIZE",
            ["purchase_units"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["custom_id"] = customId,
                    ["invoice_id"] = invoiceId,
                    ["amount"] = Money(amount, currency)
                }
            },
            ["payment_source"] = new Dictionary<string, object?>
            {
                ["card"] = new Dictionary<string, object?>
                {
                    ["vault_id"] = vaultId,
                    ["attributes"] = new Dictionary<string, object?>
                    {
                        ["verification"] = new Dictionary<string, object?> { ["method"] = "SCA_WHEN_REQUIRED" }
                    }
                }
            }
        };
        return await AuthorizeOrderAsync(body, idempotencyKey, ct);
    }

    private async Task<PayPalAuthorizationResult> AuthorizeOrderAsync(object body, string idempotencyKey, CancellationToken ct)
    {
        var root = await SendAsync(HttpMethod.Post, "/v2/checkout/orders", body, idempotencyKey, ct);

        if (root.TryGetProperty("status", out var orderStatusEl) &&
            orderStatusEl.GetString() == "PAYER_ACTION_REQUIRED")
        {
            throw new PayPalBrowserChallengeException(
                "PayPal requires the buyer to approve this payment in a browser (PAYER_ACTION_REQUIRED). " +
                "This integration is direct-card / server-to-server only.");
        }
        if (root.TryGetProperty("links", out var linksEl) && linksEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in linksEl.EnumerateArray())
            {
                if (link.TryGetProperty("rel", out var rel) && rel.GetString() == "payer-action")
                {
                    throw new PayPalBrowserChallengeException(
                        "PayPal returned a payer-action link, meaning the buyer must approve this payment in a browser. " +
                        "This integration is direct-card / server-to-server only.");
                }
            }
        }

        var payPalOrderId = root.GetProperty("id").GetString()!;
        var authorization = root.GetProperty("purchase_units")[0].GetProperty("payments").GetProperty("authorizations")[0];
        var authorizationId = authorization.GetProperty("id").GetString()!;
        var authorizationStatus = authorization.GetProperty("status").GetString()!;

        string? cardBrand = null;
        string? cardLast4 = null;
        if (root.TryGetProperty("payment_source", out var paymentSource) &&
            paymentSource.TryGetProperty("card", out var cardEl))
        {
            cardBrand = GetStringOrNull(cardEl, "brand");
            cardLast4 = GetStringOrNull(cardEl, "last_digits");
        }

        return new PayPalAuthorizationResult(payPalOrderId, authorizationId, authorizationStatus, cardBrand, cardLast4);
    }

    public async Task<PayPalCaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency,
        string invoiceId, string idempotencyKey, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["amount"] = Money(amount, currency),
            ["final_capture"] = true,
            ["invoice_id"] = invoiceId
        };
        var root = await SendAsync(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/capture", body, idempotencyKey, ct);

        var captureId = root.GetProperty("id").GetString()!;
        var status = root.GetProperty("status").GetString()!;
        var breakdown = root.GetProperty("seller_receivable_breakdown");
        var gross = decimal.Parse(breakdown.GetProperty("gross_amount").GetProperty("value").GetString()!, CultureInfo.InvariantCulture);
        var fee = decimal.Parse(breakdown.GetProperty("paypal_fee").GetProperty("value").GetString()!, CultureInfo.InvariantCulture);
        var net = decimal.Parse(breakdown.GetProperty("net_amount").GetProperty("value").GetString()!, CultureInfo.InvariantCulture);
        var createTime = root.TryGetProperty("create_time", out var ct2) && ct2.GetString() is { } createTimeStr
            ? DateTimeOffset.Parse(createTimeStr, CultureInfo.InvariantCulture)
            : DateTimeOffset.UtcNow;

        return new PayPalCaptureResult(captureId, status, gross, fee, net, createTime);
    }

    public async Task<PayPalReauthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["amount"] = Money(amount, currency) };
        var root = await SendAsync(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/reauthorize", body, null, ct);
        return new PayPalReauthorizationResult(root.GetProperty("id").GetString()!, root.GetProperty("status").GetString()!);
    }

    public async Task VoidAsync(string authorizationId, CancellationToken ct = default)
    {
        await SendRawAsync(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/void", null, null, ct);
    }

    public async Task<PayPalRefundResult> RefundAsync(string captureId, decimal amount, string currency, string customId,
        string invoiceId, string idempotencyKey, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["amount"] = Money(amount, currency),
            ["invoice_id"] = invoiceId,
            ["custom_id"] = customId
        };
        var root = await SendAsync(HttpMethod.Post, $"/v2/payments/captures/{captureId}/refund", body, idempotencyKey, ct);
        return new PayPalRefundResult(root.GetProperty("id").GetString()!, root.GetProperty("status").GetString()!);
    }

    public async Task<PayPalSetupTokenResult> CreateSetupTokenAsync(PayPalCardDetails card, string idempotencyKey,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["payment_source"] = new Dictionary<string, object?>
            {
                ["card"] = new Dictionary<string, object?>
                {
                    ["number"] = card.Number,
                    ["expiry"] = card.ExpiryYearMonth,
                    ["name"] = card.CardholderName,
                    ["billing_address"] = BillingAddress(card),
                    ["verification_method"] = "SCA_WHEN_REQUIRED",
                    // return_url/cancel_url are required by PayPal for setup tokens even though,
                    // with SETUP_NOW and a non-3DS card, no browser round-trip actually happens -
                    // confirmed live: omitting experience_context leaves the token stuck at
                    // status "CREATED" and payment-tokens then rejects it as not approved.
                    ["experience_context"] = new Dictionary<string, object?>
                    {
                        ["return_url"] = "https://localhost/paypal/return",
                        ["cancel_url"] = "https://localhost/paypal/cancel",
                        ["user_action"] = "SETUP_NOW"
                    }
                }
            }
        };
        var root = await SendAsync(HttpMethod.Post, "/v3/vault/setup-tokens", body, idempotencyKey, ct);

        var status = root.GetProperty("status").GetString()!;
        if (status != "APPROVED")
        {
            throw new PayPalBrowserChallengeException(
                $"PayPal did not approve the card synchronously (status={status}); saving this card would require a " +
                "browser approval step, which this integration does not implement.");
        }

        var setupTokenId = root.GetProperty("id").GetString()!;
        var customerId = root.GetProperty("customer").GetProperty("id").GetString()!;
        string? brand = null, last4 = null, expiry = null;
        if (root.TryGetProperty("payment_source", out var ps) && ps.TryGetProperty("card", out var cardEl))
        {
            brand = GetStringOrNull(cardEl, "brand");
            last4 = GetStringOrNull(cardEl, "last_digits");
            expiry = GetStringOrNull(cardEl, "expiry");
        }
        return new PayPalSetupTokenResult(setupTokenId, customerId, brand, last4, expiry);
    }

    public async Task<PayPalPaymentTokenResult> CreatePaymentTokenAsync(string setupTokenId, string idempotencyKey,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["payment_source"] = new Dictionary<string, object?>
            {
                ["token"] = new Dictionary<string, object?> { ["id"] = setupTokenId, ["type"] = "SETUP_TOKEN" }
            }
        };
        var root = await SendAsync(HttpMethod.Post, "/v3/vault/payment-tokens", body, idempotencyKey, ct);

        var vaultId = root.GetProperty("id").GetString()!;
        var customerId = root.GetProperty("customer").GetProperty("id").GetString()!;
        string? brand = null, last4 = null, expiry = null;
        if (root.TryGetProperty("payment_source", out var ps) && ps.TryGetProperty("card", out var cardEl))
        {
            brand = GetStringOrNull(cardEl, "brand");
            last4 = GetStringOrNull(cardEl, "last_digits");
            expiry = GetStringOrNull(cardEl, "expiry");
        }
        return new PayPalPaymentTokenResult(vaultId, customerId, brand, last4, expiry);
    }

    public async Task DeletePaymentTokenAsync(string vaultId, CancellationToken ct = default)
    {
        await SendRawAsync(HttpMethod.Delete, $"/v3/vault/payment-tokens/{vaultId}", null, null, ct);
    }

    public async Task<PayPalTransactionPage> ListTransactionsAsync(DateTimeOffset startUtc, DateTimeOffset endUtc,
        int page, int pageSize, CancellationToken ct = default)
    {
        var start = Uri.EscapeDataString(FormatTransactionDate(startUtc));
        var end = Uri.EscapeDataString(FormatTransactionDate(endUtc));
        var path = $"/v1/reporting/transactions?start_date={start}&end_date={end}&fields=all&page={page}&page_size={pageSize}";
        var root = await SendAsync(HttpMethod.Get, path, null, null, ct);

        var records = new List<PayPalTransactionRecord>();
        if (root.TryGetProperty("transaction_details", out var details) && details.ValueKind == JsonValueKind.Array)
        {
            foreach (var detail in details.EnumerateArray())
            {
                var info = detail.GetProperty("transaction_info");
                var amountEl = info.GetProperty("transaction_amount");
                var amount = decimal.Parse(amountEl.GetProperty("value").GetString()!, CultureInfo.InvariantCulture);
                decimal? fee = null;
                if (info.TryGetProperty("fee_amount", out var feeEl) && feeEl.TryGetProperty("value", out var feeVal))
                {
                    fee = decimal.Parse(feeVal.GetString()!, CultureInfo.InvariantCulture);
                }
                records.Add(new PayPalTransactionRecord(
                    info.GetProperty("transaction_id").GetString()!,
                    GetStringOrNull(info, "paypal_reference_id"),
                    GetStringOrNull(info, "transaction_status") ?? "",
                    Math.Abs(amount),
                    fee is null ? null : Math.Abs(fee.Value),
                    amountEl.GetProperty("currency_code").GetString()!,
                    DateTimeOffset.Parse(info.GetProperty("transaction_initiation_date").GetString()!, CultureInfo.InvariantCulture),
                    GetStringOrNull(info, "invoice_id"),
                    GetStringOrNull(info, "custom_field")));
            }
        }
        var totalPages = root.TryGetProperty("total_pages", out var tp) ? tp.GetInt32() : 1;
        return new PayPalTransactionPage(records, page, totalPages);
    }

    private static string FormatTransactionDate(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "Z";

    private static Dictionary<string, object?> Money(decimal amount, string currency) => new()
    {
        ["currency_code"] = currency,
        ["value"] = amount.ToString("F2", CultureInfo.InvariantCulture)
    };

    private static Dictionary<string, object?> BillingAddress(PayPalCardDetails card) => new()
    {
        ["address_line_1"] = card.AddressLine1,
        ["admin_area_2"] = card.City,
        ["admin_area_1"] = card.State,
        ["postal_code"] = card.PostalCode,
        ["country_code"] = card.CountryCode
    };

    private static string? GetStringOrNull(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body, string? idempotencyKey, CancellationToken ct)
    {
        var (statusCode, content) = await SendRawAsync(method, path, body, idempotencyKey, ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            return JsonDocument.Parse("{}").RootElement;
        }
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.Clone();
    }

    private async Task<(int StatusCode, string Content)> SendRawAsync(HttpMethod method, string path, object? body,
        string? idempotencyKey, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        using var request = new HttpRequestMessage(method, $"{_settings.ResolvedBaseUrl()}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("PayPal-Request-Id", idempotencyKey);
        }
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("PayPal {0} {1} -> {2}", method, path, (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            throw MapError(responseBody, (int)response.StatusCode);
        }
        return ((int)response.StatusCode, responseBody);
    }

    private PayPalOperationException MapError(string body, int statusCode)
    {
        string name = $"HTTP_{statusCode}";
        string message = "PayPal request failed.";
        string? debugId = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            name = GetStringOrNull(root, "name") ?? name;
            message = GetStringOrNull(root, "message") ?? message;
            debugId = GetStringOrNull(root, "debug_id");

            // "details" is safe, non-sensitive metadata (issue codes/field names/descriptions) -
            // never card data - and turns a generic error like UNPROCESSABLE_ENTITY into
            // something an operator can actually act on (e.g. DUPLICATE_INVOICE_ID).
            if (root.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
            {
                var reasons = new List<string>();
                foreach (var detail in details.EnumerateArray())
                {
                    var issue = GetStringOrNull(detail, "issue");
                    var description = GetStringOrNull(detail, "description");
                    if (issue is not null)
                    {
                        reasons.Add(description is not null ? $"{issue}: {description}" : issue);
                    }
                }
                if (reasons.Count > 0)
                {
                    message = $"{message} ({string.Join("; ", reasons)})";
                }
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body - fall back to the generic message above.
        }
        _logger.LogWarning("PayPal error {0} (status {1}, debug_id {2})", name, statusCode, debugId ?? "n/a");
        return new PayPalOperationException(name, message, debugId);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
        {
            return _cachedToken;
        }

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            {
                return _cachedToken;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.ResolvedBaseUrl()}/v1/oauth2/token");
            var basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });

            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("PayPal POST /v1/oauth2/token -> {0}", (int)response.StatusCode);
            if (!response.IsSuccessStatusCode)
            {
                throw MapError(body, (int)response.StatusCode);
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var accessToken = root.GetProperty("access_token").GetString()!;
            var expiresIn = root.GetProperty("expires_in").GetInt32();

            _cachedToken = accessToken;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(expiresIn - 60, 30));
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}
