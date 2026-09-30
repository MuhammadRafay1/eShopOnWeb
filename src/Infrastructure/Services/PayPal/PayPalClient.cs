using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Services.PayPal;

/// <summary>
/// Typed HttpClient implementation of <see cref="IPayPalClient"/>. Handles OAuth token
/// caching, idempotency headers, JSON (de)serialization of only the fields this
/// integration needs, and centralized error translation into <see cref="PayPalApiException"/>.
///
/// Never logs request/response bodies (the outbound authorize / vault bodies carry card
/// data); only the PayPal debug_id is logged on failure for operator follow-up.
/// </summary>
public class PayPalClient : IPayPalClient
{
    private const string TokenCacheKey = "paypal:access_token";

    private readonly HttpClient _http;
    private readonly PayPalOptions _options;
    private readonly IMemoryCache _cache;
    private readonly IAppLogger<PayPalClient> _logger;
    private readonly string _baseUrl;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public PayPalClient(HttpClient http, IOptions<PayPalOptions> options, IMemoryCache cache, IAppLogger<PayPalClient> logger)
    {
        _http = http;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
        _baseUrl = _options.ResolveBaseUrl();
    }

    // ---------------------------------------------------------------- Orders / Payments

    public async Task<PayPalAuthorizationResult> AuthorizeOrderAsync(PayPalAuthorizeOrderRequest request, CancellationToken ct)
    {
        object card = request.VaultId is not null
            ? new { vault_id = request.VaultId }
            : BuildCardObject(request.Card!);

        var body = new
        {
            intent = "AUTHORIZE",
            purchase_units = new[]
            {
                new
                {
                    invoice_id = request.InvoiceId,
                    custom_id = request.CustomId,
                    amount = new { currency_code = request.Currency, value = FormatAmount(request.Amount) }
                }
            },
            payment_source = new { card }
        };

        using var doc = await SendJsonAsync(HttpMethod.Post, "/v2/checkout/orders", body, request.RequestId, ct);
        var root = doc.RootElement;

        var orderId = GetString(root, "id") ?? string.Empty;
        var orderStatus = GetString(root, "status") ?? string.Empty;

        // STOP condition: a browser approval / 3DS challenge rather than a completed authorization.
        if (RequiresPayerAction(root, orderStatus))
        {
            throw new PayPalChallengeRequiredException(
                $"PayPal requires buyer approval in a browser for order {orderId} (status {orderStatus}). " +
                "This integration does not perform browser approval round-trips.");
        }

        if (!TryGetFirstAuthorization(root, out var auth))
        {
            throw new PayPalApiException(502, "NO_AUTHORIZATION",
                $"PayPal order {orderId} (status {orderStatus}) returned no authorization.", null, null);
        }

        return new PayPalAuthorizationResult
        {
            PayPalOrderId = orderId,
            AuthorizationId = GetString(auth, "id") ?? string.Empty,
            Status = GetString(auth, "status") ?? orderStatus,
            ExpiresAt = GetDate(auth, "expiration_time")
        };
    }

    public async Task<PayPalAuthorizationDetails> GetAuthorizationAsync(string authorizationId, CancellationToken ct)
    {
        using var doc = await SendJsonAsync(HttpMethod.Get, $"/v2/payments/authorizations/{authorizationId}", null, null, ct);
        var root = doc.RootElement;
        return new PayPalAuthorizationDetails
        {
            Id = GetString(root, "id") ?? authorizationId,
            Status = GetString(root, "status") ?? string.Empty,
            ExpiresAt = GetDate(root, "expiration_time")
        };
    }

    public async Task<PayPalAuthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string requestId, CancellationToken ct)
    {
        var body = new { amount = new { currency_code = currency, value = FormatAmount(amount) } };
        using var doc = await SendJsonAsync(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/reauthorize", body, requestId, ct);
        var root = doc.RootElement;
        return new PayPalAuthorizationResult
        {
            PayPalOrderId = string.Empty,
            AuthorizationId = GetString(root, "id") ?? authorizationId,
            Status = GetString(root, "status") ?? string.Empty,
            ExpiresAt = GetDate(root, "expiration_time")
        };
    }

    public async Task VoidAuthorizationAsync(string authorizationId, CancellationToken ct)
    {
        using var _ = await SendJsonAsync(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/void", null, null, ct);
    }

    public async Task<PayPalCaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency, string invoiceId, string requestId, CancellationToken ct)
    {
        var body = new
        {
            amount = new { currency_code = currency, value = FormatAmount(amount) },
            final_capture = true,
            invoice_id = invoiceId
        };
        using var doc = await SendJsonAsync(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/capture", body, requestId, ct);
        var root = doc.RootElement;

        decimal gross = amount;
        decimal? fee = null;
        decimal? net = null;
        if (root.TryGetProperty("seller_receivable_breakdown", out var breakdown))
        {
            gross = GetMoney(breakdown, "gross_amount") ?? amount;
            fee = GetMoney(breakdown, "paypal_fee");
            net = GetMoney(breakdown, "net_amount");
        }

        return new PayPalCaptureResult
        {
            CaptureId = GetString(root, "id") ?? string.Empty,
            Status = GetString(root, "status") ?? string.Empty,
            GrossAmount = gross,
            PayPalFee = fee,
            NetAmount = net
        };
    }

    public async Task<PayPalRefundResult> RefundAsync(string captureId, decimal? amount, string? currency, string requestId, CancellationToken ct)
    {
        object body = amount is null
            ? new { }
            : new { amount = new { currency_code = currency, value = FormatAmount(amount.Value) } };

        using var doc = await SendJsonAsync(HttpMethod.Post, $"/v2/payments/captures/{captureId}/refund", body, requestId, ct);
        var root = doc.RootElement;

        decimal refundedAmount = amount ?? (GetMoney(root, "amount") ?? 0m);
        decimal? totalRefunded = null;
        if (root.TryGetProperty("seller_payable_breakdown", out var breakdown))
        {
            totalRefunded = GetMoney(breakdown, "total_refunded_amount");
            var g = GetMoney(breakdown, "gross_amount");
            if (g.HasValue) refundedAmount = g.Value;
        }

        return new PayPalRefundResult
        {
            RefundId = GetString(root, "id") ?? string.Empty,
            Status = GetString(root, "status") ?? string.Empty,
            Amount = refundedAmount,
            TotalRefunded = totalRefunded
        };
    }

    // ---------------------------------------------------------------- Vault

    public async Task<PayPalVaultToken> CreateVaultedCardAsync(PayPalCardDetails card, string customerId, CancellationToken ct)
    {
        // Step 1: create a setup token from the raw card.
        var setupBody = new { payment_source = new { card = BuildCardObject(card) } };
        string setupTokenId;
        using (var setupDoc = await SendJsonAsync(HttpMethod.Post, "/v3/vault/setup-tokens", setupBody, Guid.NewGuid().ToString("N"), ct))
        {
            setupTokenId = GetString(setupDoc.RootElement, "id")
                ?? throw new PayPalApiException(502, "NO_SETUP_TOKEN", "PayPal returned no setup token id.", null, null);
        }

        // Step 2: exchange the setup token for a durable payment (vault) token.
        var tokenBody = new
        {
            payment_source = new { token = new { id = setupTokenId, type = "SETUP_TOKEN" } },
            customer = new { id = SanitizeCustomerId(customerId) }
        };
        using var tokenDoc = await SendJsonAsync(HttpMethod.Post, "/v3/vault/payment-tokens", tokenBody, Guid.NewGuid().ToString("N"), ct);
        return ParseVaultToken(tokenDoc.RootElement)
            ?? throw new PayPalApiException(502, "NO_PAYMENT_TOKEN", "PayPal returned no payment token id.", null, null);
    }

    public async Task<IReadOnlyList<PayPalVaultToken>> ListVaultedCardsAsync(string customerId, CancellationToken ct)
    {
        var results = new List<PayPalVaultToken>();
        var safeId = Uri.EscapeDataString(SanitizeCustomerId(customerId));
        const int pageSize = 50;
        int page = 1;
        int? totalPages = null;

        while (true)
        {
            var path = $"/v3/vault/payment-tokens?customer_id={safeId}&page_size={pageSize}&page={page}&total_required=true";
            using var doc = await SendJsonAsync(HttpMethod.Get, path, null, null, ct);
            var root = doc.RootElement;

            if (root.TryGetProperty("payment_tokens", out var tokens) && tokens.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in tokens.EnumerateArray())
                {
                    var parsed = ParseVaultToken(t);
                    if (parsed is not null) results.Add(parsed);
                }
            }

            if (totalPages is null && root.TryGetProperty("total_pages", out var tp) && tp.ValueKind == JsonValueKind.Number)
            {
                totalPages = tp.GetInt32();
            }

            bool more = totalPages.HasValue ? page < totalPages.Value : false;
            if (!more) break;
            page++;
        }

        return results;
    }

    public async Task DeleteVaultedCardAsync(string vaultTokenId, CancellationToken ct)
    {
        using var _ = await SendJsonAsync(HttpMethod.Delete, $"/v3/vault/payment-tokens/{vaultTokenId}", null, null, ct);
    }

    // ---------------------------------------------------------------- Transaction search

    public async Task<PayPalTransactionSearchPage> SearchTransactionsAsync(DateTimeOffset start, DateTimeOffset end, int page, int pageSize, CancellationToken ct)
    {
        var startStr = Uri.EscapeDataString(start.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        var endStr = Uri.EscapeDataString(end.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        var path = $"/v1/reporting/transactions?start_date={startStr}&end_date={endStr}" +
                   $"&fields=transaction_info&page_size={pageSize}&page={page}&balance_affecting_records_only=N";

        using var doc = await SendJsonAsync(HttpMethod.Get, path, null, null, ct);
        var root = doc.RootElement;

        var list = new List<PayPalTransaction>();
        if (root.TryGetProperty("transaction_details", out var details) && details.ValueKind == JsonValueKind.Array)
        {
            foreach (var d in details.EnumerateArray())
            {
                if (!d.TryGetProperty("transaction_info", out var info)) continue;
                list.Add(new PayPalTransaction
                {
                    TransactionId = GetString(info, "transaction_id") ?? string.Empty,
                    InvoiceId = GetString(info, "invoice_id"),
                    CustomField = GetString(info, "custom_field"),
                    Amount = GetMoney(info, "transaction_amount") ?? 0m,
                    Currency = GetMoneyCurrency(info, "transaction_amount"),
                    FeeAmount = GetMoney(info, "fee_amount"),
                    Status = GetString(info, "transaction_status"),
                    EventCode = GetString(info, "transaction_event_code"),
                    InitiationDate = GetDate(info, "transaction_initiation_date")
                });
            }
        }

        int? totalPages = null;
        if (root.TryGetProperty("total_pages", out var tp) && tp.ValueKind == JsonValueKind.Number)
        {
            totalPages = tp.GetInt32();
        }

        return new PayPalTransactionSearchPage { Transactions = list, Page = page, TotalPages = totalPages };
    }

    // ---------------------------------------------------------------- HTTP plumbing

    private async Task<JsonDocument> SendJsonAsync(HttpMethod method, string path, object? body, string? requestId, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);

        async Task<HttpResponseMessage> Send()
        {
            using var req = new HttpRequestMessage(method, _baseUrl + path);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrEmpty(requestId))
            {
                req.Headers.TryAddWithoutValidation("PayPal-Request-Id", requestId);
            }
            // Mutating calls: ask for the full representation so we get ids/status back.
            if (method != HttpMethod.Get && method != HttpMethod.Delete)
            {
                req.Headers.TryAddWithoutValidation("Prefer", "return=representation");
            }
            if (body is not null)
            {
                req.Content = new StringContent(JsonSerializer.Serialize(body, SerializerOptions), Encoding.UTF8, "application/json");
            }
            return await _http.SendAsync(req, ct);
        }

        var response = await Send();

        // One reactive refresh on a 401 (token may have expired mid-flight).
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            _cache.Remove(TokenCacheKey);
            token = await GetAccessTokenAsync(ct);
            response = await Send();
        }

        try
        {
            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw ParseError((int)response.StatusCode, content);
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return JsonDocument.Parse("{}");
            }
            return JsonDocument.Parse(content);
        }
        finally
        {
            response.Dispose();
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(TokenCacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
        {
            return cached!;
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/v1/oauth2/token");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials") });

        using var response = await _http.SendAsync(req, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw ParseError((int)response.StatusCode, content);
        }

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var accessToken = GetString(root, "access_token")
            ?? throw new PayPalApiException(502, "NO_ACCESS_TOKEN", "PayPal token response contained no access_token.", null, null);
        var expiresIn = root.TryGetProperty("expires_in", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : 3600;

        // Refresh proactively a short margin before expiry.
        var ttl = TimeSpan.FromSeconds(Math.Max(60, expiresIn - 60));
        _cache.Set(TokenCacheKey, accessToken, ttl);
        return accessToken;
    }

    private PayPalApiException ParseError(int statusCode, string content)
    {
        string? name = null, message = null, debugId = null;
        var details = new List<PayPalErrorDetail>();

        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                name = GetString(root, "name");
                message = GetString(root, "message");
                debugId = GetString(root, "debug_id");
                // OAuth errors use error / error_description instead.
                name ??= GetString(root, "error");
                message ??= GetString(root, "error_description");

                if (root.TryGetProperty("details", out var det) && det.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in det.EnumerateArray())
                    {
                        details.Add(new PayPalErrorDetail
                        {
                            Issue = GetString(d, "issue"),
                            Field = GetString(d, "field"),
                            Value = GetString(d, "value"),
                            Description = GetString(d, "description")
                        });
                    }
                }
            }
            catch (JsonException)
            {
                // Non-JSON error body — keep it opaque (do not echo, could be large/sensitive).
            }
        }

        _logger.LogWarning($"PayPal API error: status={statusCode} name={name ?? "(none)"} debug_id={debugId ?? "(none)"}");
        return new PayPalApiException(statusCode, name, message ?? name ?? $"PayPal returned HTTP {statusCode}.", debugId, details);
    }

    // ---------------------------------------------------------------- helpers

    private static object BuildCardObject(PayPalCardDetails card)
    {
        var dict = new Dictionary<string, object?>
        {
            ["number"] = card.Number,
            ["expiry"] = card.Expiry
        };
        if (!string.IsNullOrEmpty(card.SecurityCode)) dict["security_code"] = card.SecurityCode;
        if (!string.IsNullOrEmpty(card.CardholderName)) dict["name"] = card.CardholderName;
        if (card.BillingAddress is not null)
        {
            var addr = new Dictionary<string, object?> { ["country_code"] = card.BillingAddress.CountryCode };
            if (!string.IsNullOrEmpty(card.BillingAddress.AddressLine1)) addr["address_line_1"] = card.BillingAddress.AddressLine1;
            if (!string.IsNullOrEmpty(card.BillingAddress.AddressLine2)) addr["address_line_2"] = card.BillingAddress.AddressLine2;
            if (!string.IsNullOrEmpty(card.BillingAddress.AdminArea2)) addr["admin_area_2"] = card.BillingAddress.AdminArea2;
            if (!string.IsNullOrEmpty(card.BillingAddress.AdminArea1)) addr["admin_area_1"] = card.BillingAddress.AdminArea1;
            if (!string.IsNullOrEmpty(card.BillingAddress.PostalCode)) addr["postal_code"] = card.BillingAddress.PostalCode;
            dict["billing_address"] = addr;
        }
        return dict;
    }

    private static PayPalVaultToken? ParseVaultToken(JsonElement element)
    {
        var id = GetString(element, "id");
        if (string.IsNullOrEmpty(id)) return null;

        string? brand = null, last = null, expiry = null;
        if (element.TryGetProperty("payment_source", out var ps) && ps.TryGetProperty("card", out var card))
        {
            brand = GetString(card, "brand");
            last = GetString(card, "last_digits");
            expiry = GetString(card, "expiry");
        }
        return new PayPalVaultToken { Id = id!, Brand = brand, LastDigits = last, Expiry = expiry };
    }

    private static bool TryGetFirstAuthorization(JsonElement orderRoot, out JsonElement auth)
    {
        auth = default;
        if (orderRoot.TryGetProperty("purchase_units", out var pus) && pus.ValueKind == JsonValueKind.Array)
        {
            foreach (var pu in pus.EnumerateArray())
            {
                if (pu.TryGetProperty("payments", out var payments)
                    && payments.TryGetProperty("authorizations", out var auths)
                    && auths.ValueKind == JsonValueKind.Array)
                {
                    foreach (var a in auths.EnumerateArray())
                    {
                        auth = a;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static bool RequiresPayerAction(JsonElement orderRoot, string status)
    {
        if (string.Equals(status, "PAYER_ACTION_REQUIRED", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (orderRoot.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in links.EnumerateArray())
            {
                var rel = GetString(link, "rel");
                if (string.Equals(rel, "payer-action", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rel, "approve", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static string FormatAmount(decimal amount) =>
        // 2 decimal places is correct for USD and the currencies used by this task.
        // Zero-decimal currencies (e.g. JPY) would need per-currency handling — out of scope here.
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static DateTimeOffset? GetDate(JsonElement element, string name)
    {
        var s = GetString(element, name);
        if (string.IsNullOrEmpty(s)) return null;
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
    }

    private static decimal? GetMoney(JsonElement parent, string moneyProperty)
    {
        if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(moneyProperty, out var money))
        {
            var value = GetString(money, "value");
            if (value is not null && decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            {
                return d;
            }
        }
        return null;
    }

    private static string? GetMoneyCurrency(JsonElement parent, string moneyProperty) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(moneyProperty, out var money)
            ? GetString(money, "currency_code")
            : null;

    /// <summary>
    /// Derives a charset-safe, deterministic PayPal customer.id from the eShop buyer id
    /// (an email). Always computed the same way so create / list / charge line up.
    /// </summary>
    private static string SanitizeCustomerId(string buyerId)
    {
        var sb = new StringBuilder(buyerId.Length);
        foreach (var c in buyerId)
        {
            sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_');
        }
        var safe = sb.ToString();
        return safe.Length > 64 ? safe.Substring(0, 64) : safe;
    }
}
