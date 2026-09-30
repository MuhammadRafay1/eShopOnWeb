using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
using Microsoft.eShopWeb.ApplicationCore.Payments;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// HTTP implementation of <see cref="IPayPalGateway"/> against the PayPal REST API (Orders v2,
/// Payments v2, Vault v3, Transaction Search v1). Manages the OAuth token (cached and refreshed),
/// attaches the PayPal-Request-Id idempotency header to every mutating call, and translates PayPal
/// error envelopes into typed exceptions. Card-bearing request/response bodies are never logged.
/// </summary>
public class PayPalGateway : IPayPalGateway
{
    private const string TokenCacheKey = "paypal:access_token";
    private static readonly SemaphoreSlim TokenLock = new(1, 1);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly PayPalOptions _options;
    private readonly ILogger<PayPalGateway> _logger;

    public PayPalGateway(HttpClient http, IMemoryCache cache, PayPalOptions options, ILogger<PayPalGateway> logger)
    {
        _http = http;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    // --- Authorize -------------------------------------------------------------------------------

    public Task<PayPalAuthorizationResult> AuthorizeWithCardAsync(decimal amount, string currency,
        string invoiceId, PaymentCard card, string requestId, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            intent = "AUTHORIZE",
            purchase_units = new[] { PurchaseUnit(invoiceId, amount, currency) },
            payment_source = new { card = CardBody(card) }
        };
        return CreateAuthorizationAsync(body, requestId, containsCard: true, cancellationToken);
    }

    public Task<PayPalAuthorizationResult> AuthorizeWithVaultAsync(decimal amount, string currency,
        string invoiceId, string vaultId, string requestId, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            intent = "AUTHORIZE",
            purchase_units = new[] { PurchaseUnit(invoiceId, amount, currency) },
            payment_source = new { card = new { vault_id = vaultId } }
        };
        return CreateAuthorizationAsync(body, requestId, containsCard: false, cancellationToken);
    }

    private async Task<PayPalAuthorizationResult> CreateAuthorizationAsync(object body, string requestId,
        bool containsCard, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v2/checkout/orders");
        AddIdempotency(request, requestId);
        request.Content = JsonContent.Create(body, options: Json);

        using var doc = await SendAsync(request, "POST /v2/checkout/orders", logBody: !containsCard, cancellationToken);
        var root = doc.RootElement;
        var status = root.GetProperty("status").GetString();

        if (status == "PAYER_ACTION_REQUIRED")
        {
            throw new PayPalChallengeRequiredException(
                "PayPal returned PAYER_ACTION_REQUIRED (a browser approval / 3-D Secure challenge). " +
                "This integration does not perform browser approval round-trips; escalate this order.");
        }

        var payPalOrderId = root.GetProperty("id").GetString()!;
        var authorization = root
            .GetProperty("purchase_units")[0]
            .GetProperty("payments")
            .GetProperty("authorizations")[0];

        var authId = authorization.GetProperty("id").GetString()!;
        var authStatus = authorization.GetProperty("status").GetString() ?? "UNKNOWN";
        DateTimeOffset? expiresAt = authorization.TryGetProperty("expiration_time", out var exp)
            ? exp.GetDateTimeOffset()
            : null;

        if (authStatus is "DENIED" or "VOIDED")
        {
            throw new PayPalPaymentDeclinedException($"PayPal did not approve the card (authorization status {authStatus}).");
        }

        return new PayPalAuthorizationResult(payPalOrderId, authId, authStatus, expiresAt);
    }

    public async Task<PayPalAuthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount,
        string currency, string requestId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"v2/payments/authorizations/{authorizationId}/reauthorize");
        AddIdempotency(request, requestId);
        request.Content = JsonContent.Create(new { amount = Money(amount, currency) }, options: Json);

        using var doc = await SendAsync(request, "POST reauthorize", logBody: true, cancellationToken);
        var root = doc.RootElement;
        var newAuthId = root.GetProperty("id").GetString()!;
        var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "UNKNOWN" : "UNKNOWN";
        DateTimeOffset? expiresAt = root.TryGetProperty("expiration_time", out var exp)
            ? exp.GetDateTimeOffset()
            : null;
        return new PayPalAuthorizationResult(string.Empty, newAuthId, status, expiresAt);
    }

    // --- Capture ---------------------------------------------------------------------------------

    public async Task<PayPalCaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency,
        string invoiceId, string requestId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"v2/payments/authorizations/{authorizationId}/capture");
        AddIdempotency(request, requestId);
        request.Headers.Add("Prefer", "return=representation");
        request.Content = JsonContent.Create(new
        {
            amount = Money(amount, currency),
            final_capture = true,
            invoice_id = invoiceId
        }, options: Json);

        using var doc = await SendAsync(request, "POST capture", logBody: true, cancellationToken);
        var root = doc.RootElement;
        var captureId = root.GetProperty("id").GetString()!;
        var status = root.GetProperty("status").GetString() ?? "UNKNOWN";

        var (gross, fee, net) = ReadReceivableBreakdown(root, "seller_receivable_breakdown", amount);
        return new PayPalCaptureResult(captureId, status, gross, fee, net);
    }

    // --- Void ------------------------------------------------------------------------------------

    public async Task VoidAsync(string authorizationId, string requestId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"v2/payments/authorizations/{authorizationId}/void");
        AddIdempotency(request, requestId);
        // No body; response is 204 No Content.
        using var doc = await SendAsync(request, "POST void", logBody: true, cancellationToken);
    }

    // --- Refund ----------------------------------------------------------------------------------

    public async Task<PayPalRefundResult> RefundAsync(string captureId, decimal? amount, string currency,
        string invoiceId, string requestId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"v2/payments/captures/{captureId}/refund");
        AddIdempotency(request, requestId);
        request.Headers.Add("Prefer", "return=representation");
        object body = amount is null
            ? new { invoice_id = invoiceId }
            : new { amount = Money(amount.Value, currency), invoice_id = invoiceId };
        request.Content = JsonContent.Create(body, options: Json);

        using var doc = await SendAsync(request, "POST refund", logBody: true, cancellationToken);
        var root = doc.RootElement;
        var refundId = root.GetProperty("id").GetString()!;
        var status = root.GetProperty("status").GetString() ?? "UNKNOWN";
        var refundedAmount = amount ?? (root.TryGetProperty("amount", out var amt) ? ReadMoney(amt) : 0m);

        decimal totalRefunded = refundedAmount;
        if (root.TryGetProperty("seller_payable_breakdown", out var breakdown) &&
            breakdown.TryGetProperty("total_refunded_amount", out var total))
        {
            totalRefunded = ReadMoney(total);
        }
        return new PayPalRefundResult(refundId, status, refundedAmount, totalRefunded);
    }

    // --- Vault -----------------------------------------------------------------------------------

    public async Task<PayPalVaultedCard> VaultCardAsync(PaymentCard card, string requestId,
        CancellationToken cancellationToken = default)
    {
        // Step 1: create a setup token from the raw card (save without purchase).
        using var setupRequest = new HttpRequestMessage(HttpMethod.Post, "v3/vault/setup-tokens");
        AddIdempotency(setupRequest, requestId + ":setup");
        // No verification_method: the sandbox business account is enabled for direct card vaulting,
        // and requesting SCA_WHEN_REQUIRED leaves the setup token CREATED (awaiting buyer approval)
        // rather than APPROVED, which cannot then be confirmed headlessly.
        setupRequest.Content = JsonContent.Create(new
        {
            payment_source = new { card = CardBody(card) }
        }, options: Json);

        string setupTokenId;
        using (var setupDoc = await SendAsync(setupRequest, "POST /v3/vault/setup-tokens", logBody: false, cancellationToken))
        {
            setupTokenId = setupDoc.RootElement.GetProperty("id").GetString()!;
        }

        // Step 2: confirm it into a reusable payment token (the vault id).
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "v3/vault/payment-tokens");
        AddIdempotency(tokenRequest, requestId + ":token");
        tokenRequest.Content = JsonContent.Create(new
        {
            payment_source = new { token = new { id = setupTokenId, type = "SETUP_TOKEN" } }
        }, options: Json);

        using var tokenDoc = await SendAsync(tokenRequest, "POST /v3/vault/payment-tokens", logBody: true, cancellationToken);
        var root = tokenDoc.RootElement;
        var vaultId = root.GetProperty("id").GetString()!;
        string? customerId = root.TryGetProperty("customer", out var cust) && cust.TryGetProperty("id", out var cid)
            ? cid.GetString()
            : null;

        var cardEl = root.GetProperty("payment_source").GetProperty("card");
        var brand = cardEl.TryGetProperty("brand", out var b) ? b.GetString() ?? "UNKNOWN" : "UNKNOWN";
        var last4 = cardEl.TryGetProperty("last_digits", out var l) ? l.GetString() ?? "0000" : "0000";
        var expiry = cardEl.TryGetProperty("expiry", out var e) ? e.GetString() ?? card.Expiry : card.Expiry;

        return new PayPalVaultedCard(vaultId, customerId, brand, last4, expiry!);
    }

    public async Task DeleteVaultedCardAsync(string vaultId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"v3/vault/payment-tokens/{vaultId}");
        await AuthorizeRequestAsync(request, cancellationToken);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var response = await _http.SendAsync(request, cancellationToken);
        sw.Stop();
        _logger.LogInformation("PayPal DELETE /v3/vault/payment-tokens/{{id}} -> {Status} in {Elapsed}ms",
            (int)response.StatusCode, sw.ElapsedMilliseconds);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowFromErrorAsync(response, cancellationToken);
        }
    }

    // --- Transaction search (reconciliation) -----------------------------------------------------

    public async Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(DateTimeOffset from,
        DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, PayPalTransaction>();

        // PayPal's Transaction Search supports at most a 31-day range per call — chunk wider ranges.
        var windowStart = from;
        while (windowStart < to)
        {
            var windowEnd = windowStart.AddDays(31);
            if (windowEnd > to) windowEnd = to;

            var page = 1;
            int totalPages;
            do
            {
                using var doc = await GetTransactionsPageAsync(windowStart, windowEnd, page, cancellationToken);
                var root = doc.RootElement;

                if (root.TryGetProperty("transaction_details", out var details) &&
                    details.ValueKind == JsonValueKind.Array)
                {
                    foreach (var detail in details.EnumerateArray())
                    {
                        var info = detail.GetProperty("transaction_info");
                        var txn = MapTransaction(info);
                        results[txn.TransactionId] = txn;
                    }
                }

                totalPages = root.TryGetProperty("total_pages", out var tp) ? tp.GetInt32() : 1;
                page++;
            }
            while (page <= totalPages);

            if (windowEnd >= to) break;
            windowStart = windowEnd;
        }

        return results.Values.ToList();
    }

    private async Task<JsonDocument> GetTransactionsPageAsync(DateTimeOffset start, DateTimeOffset end,
        int page, CancellationToken cancellationToken)
    {
        var url = "v1/reporting/transactions" +
                  $"?start_date={Uri.EscapeDataString(FormatReportDate(start))}" +
                  $"&end_date={Uri.EscapeDataString(FormatReportDate(end))}" +
                  "&fields=transaction_info&page_size=500&page=" + page;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendAsync(request, "GET /v1/reporting/transactions", logBody: true, cancellationToken);
    }

    private static PayPalTransaction MapTransaction(JsonElement info)
    {
        string transactionId = info.GetProperty("transaction_id").GetString()!;
        string? referenceId = info.TryGetProperty("paypal_reference_id", out var r) ? r.GetString() : null;
        string? referenceType = info.TryGetProperty("paypal_reference_id_type", out var rt) ? rt.GetString() : null;
        string? invoiceId = info.TryGetProperty("invoice_id", out var inv) ? inv.GetString() : null;
        string? status = info.TryGetProperty("transaction_status", out var st) ? st.GetString() : null;

        decimal amount = 0m;
        string currency = "";
        if (info.TryGetProperty("transaction_amount", out var amt))
        {
            amount = ReadMoney(amt);
            currency = amt.TryGetProperty("currency_code", out var cc) ? cc.GetString() ?? "" : "";
        }
        decimal? fee = info.TryGetProperty("fee_amount", out var f) ? ReadMoney(f) : null;
        DateTimeOffset? date = info.TryGetProperty("transaction_initiation_date", out var d) && d.ValueKind == JsonValueKind.String
            ? d.GetDateTimeOffset()
            : null;

        return new PayPalTransaction(transactionId, referenceId, referenceType, invoiceId, amount, currency, fee, status, date);
    }

    // --- HTTP plumbing ---------------------------------------------------------------------------

    private static object PurchaseUnit(string invoiceId, decimal amount, string currency) => new
    {
        invoice_id = invoiceId,
        amount = Money(amount, currency)
    };

    private static object Money(decimal amount, string currency) => new
    {
        currency_code = currency,
        value = amount.ToString("0.00", CultureInfo.InvariantCulture)
    };

    private static object CardBody(PaymentCard card) => new
    {
        number = card.Number,
        expiry = card.Expiry,
        security_code = card.SecurityCode,
        name = card.Name,
        billing_address = new
        {
            address_line_1 = card.AddressLine1,
            address_line_2 = card.AddressLine2,
            admin_area_2 = card.City,
            admin_area_1 = card.State,
            postal_code = card.PostalCode,
            country_code = card.CountryCode
        }
    };

    private static (decimal gross, decimal fee, decimal net) ReadReceivableBreakdown(
        JsonElement root, string breakdownName, decimal fallbackGross)
    {
        if (root.TryGetProperty(breakdownName, out var breakdown))
        {
            decimal gross = breakdown.TryGetProperty("gross_amount", out var g) ? ReadMoney(g) : fallbackGross;
            decimal fee = breakdown.TryGetProperty("paypal_fee", out var f) ? ReadMoney(f) : 0m;
            decimal net = breakdown.TryGetProperty("net_amount", out var n) ? ReadMoney(n) : gross - fee;
            return (gross, fee, net);
        }
        return (fallbackGross, 0m, fallbackGross);
    }

    private static decimal ReadMoney(JsonElement money)
    {
        if (money.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String &&
            decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }
        return 0m;
    }

    private void AddIdempotency(HttpRequestMessage request, string requestId)
        => request.Headers.Add("PayPal-Request-Id", requestId);

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, string label, bool logBody,
        CancellationToken cancellationToken)
    {
        await AuthorizeRequestAsync(request, cancellationToken);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var response = await _http.SendAsync(request, cancellationToken);
        sw.Stop();
        // Only method/path/status/elapsed are logged — never request or response bodies, so card
        // details can never reach the logs.
        _logger.LogInformation("PayPal {Label} -> {Status} in {Elapsed}ms", label, (int)response.StatusCode, sw.ElapsedMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowFromErrorAsync(response, cancellationToken);
        }

        if (response.StatusCode == HttpStatusCode.NoContent ||
            response.Content.Headers.ContentLength is null or 0)
        {
            return JsonDocument.Parse("{}");
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private async Task ThrowFromErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;
        string? name = null, message = null, debugId = null, issue = null, description = null;
        try
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(payload))
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
                message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
                debugId = root.TryGetProperty("debug_id", out var d) ? d.GetString() : null;
                if (root.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array &&
                    details.GetArrayLength() > 0)
                {
                    var first = details[0];
                    issue = first.TryGetProperty("issue", out var i) ? i.GetString() : null;
                    description = first.TryGetProperty("description", out var de) ? de.GetString() : null;
                }
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body — fall through with what we have.
        }

        if (issue == "AUTHORIZATION_EXPIRED")
        {
            throw new PayPalAuthorizationExpiredException(issue, description);
        }
        if (IsDecline(issue))
        {
            throw new PayPalPaymentDeclinedException($"PayPal declined the card: {issue} - {description}");
        }
        throw new PayPalApiException(statusCode, name, message, debugId, issue, description);
    }

    private static bool IsDecline(string? issue) => issue is
        "INSTRUMENT_DECLINED" or "PAYER_CANNOT_PAY" or "PROCESSOR_DECLINE" or
        "CARD_CLOSED" or "TRANSACTION_REFUSED" or "CARD_EXPIRED" or "PAYMENT_DENIED";

    // --- OAuth token -----------------------------------------------------------------------------

    private async Task AuthorizeRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(TokenCacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
        {
            return cached!;
        }

        await TokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(TokenCacheKey, out cached) && !string.IsNullOrEmpty(cached))
            {
                return cached!;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token");
            var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await ThrowFromErrorAsync(response, cancellationToken);
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var token = doc.RootElement.GetProperty("access_token").GetString()!;
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;

            // Refresh a minute before expiry to avoid using a token that lapses mid-request.
            var ttl = TimeSpan.FromSeconds(Math.Max(60, expiresIn - 60));
            _cache.Set(TokenCacheKey, token, ttl);
            return token;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private static string FormatReportDate(DateTimeOffset value)
        => value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss-0000", CultureInfo.InvariantCulture);
}
