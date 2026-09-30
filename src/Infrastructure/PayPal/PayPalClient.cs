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
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.Extensions.Caching.Memory;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// A hand-written client built against the PayPal OpenAPI specs (no third-party SDK). Implements
/// the ordering, payments, vault and reporting seams; shares a single cached OAuth2 token.
/// </summary>
public class PayPalClient : IPayPalOrdersClient, IPayPalPaymentsClient, IPayPalVaultClient, IPayPalReportingClient
{
    private const string TokenCacheKey = "paypal:access_token";

    private readonly HttpClient _http;
    private readonly PayPalOptions _options;
    private readonly IMemoryCache _cache;
    private readonly IAppLogger<PayPalClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public PayPalClient(HttpClient http, PayPalOptions options, IMemoryCache cache,
        IAppLogger<PayPalClient> logger)
    {
        _http = http;
        _options = options;
        _cache = cache;
        _logger = logger;
    }

    // ----------------------------------------------------------------- Orders v2

    public async Task<PayPalAuthorizeResult> CreateAuthorizedOrderAsync(decimal amount,
        string eShopOrderId, PayPalCard? card, string? vaultId, string requestId,
        CancellationToken cancellationToken = default)
    {
        if (card is null && string.IsNullOrEmpty(vaultId))
            throw new ArgumentException("Either card details or a vault id must be supplied.");

        var body = new CreateOrderRequest
        {
            Intent = "AUTHORIZE",
            PurchaseUnits = new List<PurchaseUnitRequest>
            {
                new PurchaseUnitRequest
                {
                    // custom_id is documented as the field to reconcile against; it appears in
                    // transaction/settlement reports and, unlike invoice_id, carries no
                    // merchant-wide uniqueness constraint (which would collide across runs).
                    Amount = MoneyOf(amount),
                    CustomId = eShopOrderId
                }
            },
            PaymentSource = new PaymentSourceRequest
            {
                Card = vaultId is not null
                    ? new CardRequest { VaultId = vaultId }
                    : ToCardRequest(card!)
            }
        };

        var order = await SendAsync<OrderResponse>(HttpMethod.Post, "v2/checkout/orders", body,
            requestId, cancellationToken);

        var auth = FindAuthorization(order);
        return new PayPalAuthorizeResult(
            PayPalOrderId: order.Id ?? string.Empty,
            OrderStatus: order.Status ?? string.Empty,
            AuthorizationId: auth?.Id ?? string.Empty,
            AuthorizationStatus: auth?.Status ?? string.Empty,
            Amount: ParseAmount(auth?.Amount) ?? amount,
            CurrencyCode: auth?.Amount?.CurrencyCode ?? _options.Currency,
            ExpiresAt: ParseDate(auth?.ExpirationTime));
    }

    // --------------------------------------------------------------- Payments v2

    public async Task<PayPalAuthorizationDetails> GetAuthorizationAsync(string authorizationId,
        CancellationToken cancellationToken = default)
    {
        var auth = await SendAsync<AuthorizationResponse>(HttpMethod.Get,
            $"v2/payments/authorizations/{authorizationId}", null, null, cancellationToken);
        return ToAuthorizationDetails(auth);
    }

    public async Task<PayPalCaptureResult> CaptureAuthorizationAsync(string authorizationId,
        decimal amount, string requestId, CancellationToken cancellationToken = default)
    {
        var body = new CaptureRequest { Amount = MoneyOf(amount), FinalCapture = true };
        var capture = await SendAsync<CaptureResponse>(HttpMethod.Post,
            $"v2/payments/authorizations/{authorizationId}/capture", body, requestId, cancellationToken);

        // The capture POST response omits seller_receivable_breakdown for some card payments;
        // GET the capture to obtain PayPal's fee and net proceeds when they aren't inline.
        if (capture.Id is not null &&
            (capture.SellerReceivableBreakdown?.PayPalFee is null || capture.SellerReceivableBreakdown?.NetAmount is null))
        {
            try
            {
                var fetched = await SendAsync<CaptureResponse>(HttpMethod.Get,
                    $"v2/payments/captures/{capture.Id}", null, null, cancellationToken);
                if (fetched.SellerReceivableBreakdown is not null)
                    capture.SellerReceivableBreakdown = fetched.SellerReceivableBreakdown;
            }
            catch (PayPalApiException ex)
            {
                _logger.LogWarning("Could not fetch capture {0} breakdown: {1}.", capture.Id, ex.Message);
            }
        }

        var breakdown = capture.SellerReceivableBreakdown;
        return new PayPalCaptureResult(
            CaptureId: capture.Id ?? string.Empty,
            Status: capture.Status ?? string.Empty,
            GrossAmount: ParseAmount(breakdown?.GrossAmount) ?? ParseAmount(capture.Amount) ?? amount,
            PayPalFee: ParseAmount(breakdown?.PayPalFee),
            NetAmount: ParseAmount(breakdown?.NetAmount),
            CurrencyCode: capture.Amount?.CurrencyCode ?? breakdown?.GrossAmount?.CurrencyCode ?? _options.Currency,
            CapturedAt: ParseDate(capture.CreateTime) ?? DateTimeOffset.UtcNow);
    }

    public async Task<PayPalAuthorizationDetails> ReauthorizeAsync(string authorizationId,
        decimal amount, string requestId, CancellationToken cancellationToken = default)
    {
        var body = new ReauthorizeRequest { Amount = MoneyOf(amount) };
        var auth = await SendAsync<AuthorizationResponse>(HttpMethod.Post,
            $"v2/payments/authorizations/{authorizationId}/reauthorize", body, requestId, cancellationToken);
        return ToAuthorizationDetails(auth);
    }

    public async Task VoidAuthorizationAsync(string authorizationId, string requestId,
        CancellationToken cancellationToken = default)
    {
        // Void returns 204 No Content; no body to deserialize.
        await SendAsync(HttpMethod.Post, $"v2/payments/authorizations/{authorizationId}/void",
            null, requestId, cancellationToken);
    }

    public async Task<PayPalRefundResult> RefundCaptureAsync(string captureId, decimal? amount,
        string eShopOrderId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        // A partial refund carries an amount; a full refund of the remaining balance can omit it,
        // but we always pass the computed amount so the request is deterministic.
        var body = new RefundRequest
        {
            Amount = amount.HasValue ? MoneyOf(amount.Value) : null,
            CustomId = eShopOrderId
        };
        var refund = await SendAsync<RefundResponse>(HttpMethod.Post,
            $"v2/payments/captures/{captureId}/refund", body, idempotencyKey, cancellationToken);

        return new PayPalRefundResult(
            RefundId: refund.Id ?? string.Empty,
            Status: refund.Status ?? string.Empty,
            Amount: ParseAmount(refund.Amount) ?? amount ?? 0m,
            CurrencyCode: refund.Amount?.CurrencyCode ?? _options.Currency);
    }

    // ------------------------------------------------------------------- Vault v3

    public async Task<PayPalVaultResult> VaultCardAsync(PayPalCard card, string merchantCustomerId,
        string? existingCustomerId, string requestId, CancellationToken cancellationToken = default)
    {
        var body = new VaultTokenRequest
        {
            Customer = existingCustomerId is not null
                ? new VaultCustomer { Id = existingCustomerId }
                : new VaultCustomer { MerchantCustomerId = merchantCustomerId },
            PaymentSource = new VaultPaymentSource
            {
                Card = new VaultCard
                {
                    Name = card.Name,
                    Number = card.Number,
                    Expiry = card.Expiry,
                    SecurityCode = card.SecurityCode,
                    BillingAddress = ToCardAddress(card.BillingAddress)
                }
            }
        };

        var token = await SendAsync<VaultTokenResponse>(HttpMethod.Post, "v3/vault/payment-tokens",
            body, requestId, cancellationToken);

        var respCard = token.PaymentSource?.Card;
        var (month, year) = ParseExpiry(respCard?.Expiry);
        return new PayPalVaultResult(
            VaultId: token.Id ?? string.Empty,
            CustomerId: token.Customer?.Id ?? existingCustomerId ?? string.Empty,
            Brand: respCard?.Brand ?? "CARD",
            Last4: respCard?.LastDigits ?? string.Empty,
            ExpiryMonth: month,
            ExpiryYear: year,
            Name: respCard?.Name);
    }

    public async Task<IReadOnlyList<PayPalVaultedCard>> ListVaultedCardsAsync(string customerId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<PayPalVaultedCard>();
        int page = 1;
        int totalPages;
        do
        {
            var path = $"v3/vault/payment-tokens?customer_id={Uri.EscapeDataString(customerId)}" +
                       $"&page_size=5&page={page}&total_required=true";
            var list = await SendAsync<VaultTokenListResponse>(HttpMethod.Get, path, null, null, cancellationToken);
            totalPages = list.TotalPages <= 0 ? 1 : list.TotalPages;
            if (list.PaymentTokens is not null)
            {
                foreach (var token in list.PaymentTokens)
                {
                    var card = token.PaymentSource?.Card;
                    if (card is null) continue;
                    var (month, year) = ParseExpiry(card.Expiry);
                    results.Add(new PayPalVaultedCard(token.Id ?? string.Empty,
                        card.Brand ?? "CARD", card.LastDigits ?? string.Empty, month, year));
                }
            }
            page++;
        } while (page <= totalPages);

        return results;
    }

    public async Task DeleteVaultedCardAsync(string vaultId, CancellationToken cancellationToken = default)
    {
        await SendAsync(HttpMethod.Delete, $"v3/vault/payment-tokens/{vaultId}", null, null, cancellationToken);
    }

    // ------------------------------------------------------- Transaction Search v1

    public async Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var results = new List<PayPalTransaction>();
        // The API caps a single call at a 31-day range; split [from, to] into consecutive windows.
        foreach (var (windowStart, windowEnd) in SplitIntoWindows(from, to, TimeSpan.FromDays(31)))
        {
            await SearchWindowAsync(windowStart, windowEnd, results, cancellationToken);
        }
        return results;
    }

    private async Task SearchWindowAsync(DateTimeOffset start, DateTimeOffset end,
        List<PayPalTransaction> sink, CancellationToken cancellationToken)
    {
        const int pageSize = 500; // spec maximum
        int page = 1;
        int totalPages;
        do
        {
            var path = "v1/reporting/transactions" +
                       $"?start_date={FormatDate(start)}&end_date={FormatDate(end)}" +
                       $"&fields=transaction_info&page_size={pageSize}&page={page}";
            var response = await SendAsync<TransactionSearchResponse>(HttpMethod.Get, path, null, null, cancellationToken);
            totalPages = response.TotalPages <= 0 ? 1 : response.TotalPages;

            if (response.TransactionDetails is not null)
            {
                foreach (var detail in response.TransactionDetails)
                {
                    var info = detail.TransactionInfo;
                    if (info is null) continue;
                    sink.Add(new PayPalTransaction(
                        TransactionId: info.TransactionId,
                        ReferenceId: info.PayPalReferenceId,
                        ReferenceIdType: info.PayPalReferenceIdType,
                        EventCode: info.TransactionEventCode,
                        Status: info.TransactionStatus,
                        Amount: ParseAmount(info.TransactionAmount),
                        CurrencyCode: info.TransactionAmount?.CurrencyCode,
                        FeeAmount: ParseAmount(info.FeeAmount),
                        InvoiceId: info.InvoiceId,
                        CustomField: info.CustomField,
                        InitiationDate: ParseDate(info.TransactionInitiationDate),
                        UpdatedDate: ParseDate(info.TransactionUpdatedDate)));
                }
            }
            page++;
        } while (page <= totalPages);
    }

    internal static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> SplitIntoWindows(
        DateTimeOffset from, DateTimeOffset to, TimeSpan maxWindow)
    {
        var start = from;
        while (start < to)
        {
            var end = start + maxWindow;
            if (end > to) end = to;
            yield return (start, end);
            start = end;
        }
        if (from == to)
            yield return (from, to);
    }

    // ------------------------------------------------------------------- Transport

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(TokenCacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
            return cached!;

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        using var response = await _http.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw ToApiException((int)response.StatusCode, payload);

        var token = JsonSerializer.Deserialize<TokenResponse>(payload, JsonOptions);
        if (token?.AccessToken is null)
            throw new PayPalApiException((int)response.StatusCode, "TOKEN_ERROR",
                "PayPal did not return an access token.", null, null);

        // Refresh a little before the reported lifetime elapses.
        var lifetime = TimeSpan.FromSeconds(Math.Max(60, token.ExpiresIn - 60));
        _cache.Set(TokenCacheKey, token.AccessToken, lifetime);
        return token.AccessToken;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body,
        string? requestId, CancellationToken cancellationToken)
    {
        var payload = await SendAsync(method, path, body, requestId, cancellationToken);
        var result = JsonSerializer.Deserialize<T>(payload, JsonOptions);
        if (result is null)
            throw new PayPalApiException(500, "DESERIALIZATION_ERROR",
                $"Could not read PayPal response for {method} {path}.", null, null);
        return result;
    }

    private async Task<string> SendAsync(HttpMethod method, string path, object? body,
        string? requestId, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!string.IsNullOrEmpty(requestId))
            request.Headers.TryAddWithoutValidation("PayPal-Request-Id", requestId);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);

        using var response = await _http.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var ex = ToApiException((int)response.StatusCode, responseBody);
            _logger.LogWarning("PayPal {0} {1} failed: {2} (debug_id {3}).",
                method, path, ex.Message, ex.DebugId ?? "-");
            throw ex;
        }

        return responseBody;
    }

    private static PayPalApiException ToApiException(int statusCode, string responseBody)
    {
        string? name = null, message = null, debugId = null;
        var issues = new List<string>();
        try
        {
            var error = JsonSerializer.Deserialize<PayPalErrorBody>(responseBody, JsonOptions);
            if (error is not null)
            {
                name = error.Name;
                message = error.Message;
                debugId = error.DebugId;
                if (error.Details is not null)
                    foreach (var d in error.Details)
                        if (!string.IsNullOrEmpty(d.Issue)) issues.Add(d.Issue!);
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body (rare) — fall through with the raw text as the message.
        }

        return new PayPalApiException(statusCode, name,
            message ?? (string.IsNullOrWhiteSpace(responseBody) ? $"HTTP {statusCode}" : responseBody),
            debugId, issues);
    }

    // ------------------------------------------------------------------- Mapping helpers

    private static AuthorizationResponse? FindAuthorization(OrderResponse order)
    {
        var units = order.PurchaseUnits;
        if (units is null) return null;
        foreach (var unit in units)
        {
            var auths = unit.Payments?.Authorizations;
            if (auths is not null && auths.Count > 0) return auths[0];
        }
        return null;
    }

    private PayPalAuthorizationDetails ToAuthorizationDetails(AuthorizationResponse auth) =>
        new PayPalAuthorizationDetails(
            AuthorizationId: auth.Id ?? string.Empty,
            Status: auth.Status ?? string.Empty,
            Amount: ParseAmount(auth.Amount) ?? 0m,
            CurrencyCode: auth.Amount?.CurrencyCode ?? _options.Currency,
            ExpiresAt: ParseDate(auth.ExpirationTime));

    private CardRequest ToCardRequest(PayPalCard card) => new CardRequest
    {
        Name = card.Name,
        Number = card.Number,
        Expiry = card.Expiry,
        SecurityCode = card.SecurityCode,
        BillingAddress = ToCardAddress(card.BillingAddress)
    };

    private static CardAddress? ToCardAddress(PayPalBillingAddress? address)
    {
        if (address is null) return null;
        return new CardAddress
        {
            AddressLine1 = address.AddressLine1,
            AddressLine2 = address.AddressLine2,
            AdminArea2 = address.AdminArea2,
            AdminArea1 = address.AdminArea1,
            PostalCode = address.PostalCode,
            CountryCode = address.CountryCode
        };
    }

    private Money MoneyOf(decimal amount) =>
        new Money { CurrencyCode = _options.Currency, Value = FormatMoney(amount) };

    private string FormatMoney(decimal amount)
    {
        var decimals = _options.Currency?.ToUpperInvariant() switch
        {
            "JPY" or "HUF" or "TWD" => 0,
            _ => 2
        };
        return amount.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    private static decimal? ParseAmount(Money? money)
    {
        if (money?.Value is null) return null;
        return decimal.TryParse(money.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value : (decimal?)null;
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed : (DateTimeOffset?)null;
    }

    private static (int Month, int Year) ParseExpiry(string? expiry)
    {
        // Format is YYYY-MM per the spec.
        if (!string.IsNullOrEmpty(expiry) && expiry!.Length == 7 && expiry[4] == '-'
            && int.TryParse(expiry.AsSpan(0, 4), out var year)
            && int.TryParse(expiry.AsSpan(5, 2), out var month))
        {
            return (month, year);
        }
        return (0, 0);
    }

    private static string FormatDate(DateTimeOffset value) =>
        // RFC 3339 with seconds, as the spec requires.
        Uri.EscapeDataString(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
}
