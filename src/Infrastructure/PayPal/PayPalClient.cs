using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Configuration;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.PayPal.Models;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Hand-written typed HttpClient built strictly against the operations declared in
/// api-specs/paypal (Checkout Orders v2, Payments v2, Vault Payment Tokens v3, Transaction
/// Search v1). No third-party PayPal SDK is used. Never logs card data, the client secret, or
/// full request bodies - only method/path/status/debug_id.
/// </summary>
public class PayPalClient : IPayPalClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly PayPalSettings _settings;
    private readonly PayPalAccessTokenCache _tokenCache;
    private readonly IAppLogger<PayPalClient> _logger;

    public PayPalClient(HttpClient httpClient, IOptions<PayPalSettings> settings, PayPalAccessTokenCache tokenCache, IAppLogger<PayPalClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _tokenCache = tokenCache;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(PayPalBaseUrlResolver.Resolve(_settings));
        }
    }

    public async Task<PayPalOrderResult> CreateOrderAsync(PayPalCreateOrderInput input, string requestId, CancellationToken cancellationToken)
    {
        CardRequestDto card;
        if (!string.IsNullOrEmpty(input.VaultId))
        {
            card = new CardRequestDto { VaultId = input.VaultId };
        }
        else if (input.Card is not null)
        {
            card = BuildCardRequest(input.Card);
            if (input.SaveCardOnSuccess)
            {
                card.Attributes = new CardAttributesDto
                {
                    Vault = new VaultInstructionDto { StoreInVault = "ON_SUCCESS" },
                    Customer = BuildCustomer(input.ExistingPayPalCustomerId, input.MerchantCustomerId)
                };
            }
        }
        else
        {
            throw new ArgumentException("PayPalCreateOrderInput must set either Card or VaultId.", nameof(input));
        }

        var body = new CreateOrderRequestDto
        {
            Intent = "AUTHORIZE",
            PurchaseUnits = new List<PurchaseUnitRequestDto>
            {
                new PurchaseUnitRequestDto
                {
                    // PayPal deduplicates invoice_id permanently within an account, even across
                    // unrelated orders/sessions. eShop's own order id is not by itself a safe
                    // uniqueness guarantee against a shared sandbox account's history (and is
                    // reset to 1 on every restart when running against the in-memory database),
                    // so a per-attempt nonce is appended; custom_id below still carries the bare
                    // order id for at-a-glance reconciliation.
                    InvoiceId = $"eshop-{input.OrderId}-{Guid.NewGuid():N}",
                    CustomId = input.OrderId.ToString(CultureInfo.InvariantCulture),
                    Amount = new MoneyDto { CurrencyCode = input.CurrencyCode, Value = FormatAmount(input.Amount) }
                }
            },
            PaymentSource = new PaymentSourceRequestDto { Card = card }
        };

        var response = await SendAsync<OrderResponseDto>(HttpMethod.Post, "/v2/checkout/orders", body, requestId, preferRepresentation: true, cancellationToken);

        return new PayPalOrderResult { PayPalOrderId = response.Id, Status = response.Status };
    }

    public async Task<PayPalAuthorizeResult> AuthorizeOrderAsync(string payPalOrderId, string requestId, CancellationToken cancellationToken)
    {
        var response = await SendAsync<OrderResponseDto>(HttpMethod.Post, $"/v2/checkout/orders/{payPalOrderId}/authorize", null, requestId, preferRepresentation: true, cancellationToken);

        var authorization = response.PurchaseUnits?
            .SelectMany(pu => pu.Payments?.Authorizations ?? new List<AuthorizationDto>())
            .FirstOrDefault();

        var requiresPayerAction = string.Equals(response.Status, "PAYER_ACTION_REQUIRED", StringComparison.OrdinalIgnoreCase)
            || (response.Links?.Any(l => string.Equals(l.Rel, "payer-action", StringComparison.OrdinalIgnoreCase)) ?? false);

        var vault = response.PaymentSource?.Card?.Attributes?.Vault;

        return new PayPalAuthorizeResult
        {
            PayPalOrderId = response.Id,
            OrderStatus = response.Status,
            AuthorizationId = authorization?.Id,
            AuthorizationStatus = authorization?.Status,
            AuthorizationExpiresAt = authorization?.ExpirationTime,
            RequiresPayerAction = requiresPayerAction,
            VaultId = vault?.Id,
            VaultCustomerId = vault?.Customer?.Id,
            CardBrand = response.PaymentSource?.Card?.Brand,
            CardLastDigits = response.PaymentSource?.Card?.LastDigits,
            CardExpiry = response.PaymentSource?.Card?.Expiry
        };
    }

    public async Task<PayPalCaptureResult> CaptureAuthorizationAsync(string authorizationId, decimal amount, string currencyCode, string requestId, CancellationToken cancellationToken)
    {
        var body = new CaptureRequestDto
        {
            Amount = new MoneyDto { CurrencyCode = currencyCode, Value = FormatAmount(amount) },
            FinalCapture = true
        };

        var response = await SendAsync<CaptureDto>(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/capture", body, requestId, preferRepresentation: true, cancellationToken);

        var gross = ParseAmount(response.SellerReceivableBreakdown?.GrossAmount) ?? ParseAmount(response.Amount) ?? amount;

        return new PayPalCaptureResult
        {
            CaptureId = response.Id,
            Status = response.Status,
            GrossAmount = gross,
            FeeAmount = ParseAmount(response.SellerReceivableBreakdown?.PaypalFee),
            NetAmount = ParseAmount(response.SellerReceivableBreakdown?.NetAmount)
        };
    }

    public async Task VoidAuthorizationAsync(string authorizationId, string requestId, CancellationToken cancellationToken)
    {
        await SendNoContentAsync(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/void", (object?)null, requestId, cancellationToken);
    }

    public async Task<PayPalAuthorizationStatusResult> ReauthorizeAsync(string authorizationId, decimal amount, string currencyCode, string requestId, CancellationToken cancellationToken)
    {
        var body = new ReauthorizeRequestDto { Amount = new MoneyDto { CurrencyCode = currencyCode, Value = FormatAmount(amount) } };

        var response = await SendAsync<AuthorizationDto>(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/reauthorize", body, requestId, preferRepresentation: true, cancellationToken);

        return new PayPalAuthorizationStatusResult
        {
            AuthorizationId = response.Id,
            Status = response.Status,
            ExpiresAt = response.ExpirationTime
        };
    }

    public async Task<PayPalRefundResult> RefundCaptureAsync(string captureId, decimal? amount, string currencyCode, string requestId, CancellationToken cancellationToken)
    {
        RefundRequestDto body = amount.HasValue
            ? new RefundRequestDto { Amount = new MoneyDto { CurrencyCode = currencyCode, Value = FormatAmount(amount.Value) } }
            : new RefundRequestDto();

        var response = await SendAsync<RefundResponseDto>(HttpMethod.Post, $"/v2/payments/captures/{captureId}/refund", body, requestId, preferRepresentation: true, cancellationToken);

        var refundedAmount = ParseAmount(response.Amount) ?? amount ?? 0m;

        return new PayPalRefundResult { RefundId = response.Id, Status = response.Status, Amount = refundedAmount };
    }

    public async Task<PayPalVaultTokenResult> CreatePaymentTokenAsync(PayPalCardInput card, string? payPalCustomerId, string? merchantCustomerId, string requestId, CancellationToken cancellationToken)
    {
        var body = new VaultCreateRequestDto
        {
            Customer = BuildCustomer(payPalCustomerId, merchantCustomerId),
            PaymentSource = new VaultPaymentSourceDto { Card = BuildCardRequest(card) }
        };

        var response = await SendAsync<VaultResponseDto>(HttpMethod.Post, "/v3/vault/payment-tokens", body, requestId, preferRepresentation: false, cancellationToken);

        var customerId = response.Customer?.Id ?? payPalCustomerId;
        if (string.IsNullOrEmpty(customerId))
        {
            throw new PayPalApiException(System.Net.HttpStatusCode.InternalServerError, "missing_customer_id",
                "PayPal's vault response did not include a customer id.", null);
        }

        return new PayPalVaultTokenResult
        {
            VaultId = response.Id,
            PayPalCustomerId = customerId,
            Brand = response.PaymentSource?.Card?.Brand,
            LastDigits = response.PaymentSource?.Card?.LastDigits,
            Expiry = response.PaymentSource?.Card?.Expiry
        };
    }

    public async Task DeletePaymentTokenAsync(string vaultTokenId, CancellationToken cancellationToken)
    {
        await SendNoContentAsync(HttpMethod.Delete, $"/v3/vault/payment-tokens/{vaultTokenId}", (object?)null, null, cancellationToken);
    }

    public async Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var results = new List<PayPalTransaction>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (start, end) in ChunkRange(from, to))
        {
            var page = 1;
            int totalPages;
            do
            {
                var path = "/v1/reporting/transactions"
                    + $"?start_date={Uri.EscapeDataString(FormatRfc3339(start))}"
                    + $"&end_date={Uri.EscapeDataString(FormatRfc3339(end))}"
                    + "&fields=transaction_info"
                    + "&page_size=500"
                    + $"&page={page}";

                var response = await SendAsync<SearchResponseDto>(HttpMethod.Get, path, (object?)null, null, preferRepresentation: false, cancellationToken);

                totalPages = Math.Max(response.TotalPages, response.TransactionDetails?.Count > 0 ? 1 : 0);

                foreach (var detail in response.TransactionDetails ?? new List<TransactionDetailDto>())
                {
                    var info = detail.TransactionInfo;
                    if (info is null || string.IsNullOrEmpty(info.TransactionId) || !seenIds.Add(info.TransactionId))
                    {
                        continue;
                    }

                    results.Add(new PayPalTransaction
                    {
                        TransactionId = info.TransactionId,
                        Status = info.TransactionStatus,
                        InvoiceId = info.InvoiceId,
                        CustomField = info.CustomField,
                        InitiationDate = info.TransactionInitiationDate,
                        Amount = ParseAmount(info.TransactionAmount),
                        CurrencyCode = info.TransactionAmount?.CurrencyCode,
                        FeeAmount = ParseAmount(info.FeeAmount)
                    });
                }

                page++;
            } while (page <= totalPages);
        }

        return results;
    }

    private static CardRequestDto BuildCardRequest(PayPalCardInput card)
    {
        return new CardRequestDto
        {
            Name = card.Name,
            Number = card.Number,
            Expiry = card.Expiry,
            SecurityCode = card.SecurityCode,
            BillingAddress = card.BillingAddress is null
                ? null
                : new BillingAddressDto
                {
                    AddressLine1 = card.BillingAddress.AddressLine1,
                    AdminArea1 = card.BillingAddress.AdminArea1,
                    AdminArea2 = card.BillingAddress.AdminArea2,
                    PostalCode = card.BillingAddress.PostalCode,
                    CountryCode = card.BillingAddress.CountryCode
                }
        };
    }

    private static CustomerDto? BuildCustomer(string? payPalCustomerId, string? merchantCustomerId)
    {
        if (string.IsNullOrEmpty(payPalCustomerId) && string.IsNullOrEmpty(merchantCustomerId))
        {
            return null;
        }
        return new CustomerDto { Id = payPalCustomerId, MerchantCustomerId = SanitizeMerchantCustomerId(merchantCustomerId) };
    }

    /// <summary>
    /// merchant_customer_id only needs to correlate back to our own buyer id for a human
    /// glancing at PayPal's dashboard - it is never used to look anything up on our side (we
    /// track the PayPal customer id ourselves on PaymentMethod). The spec's own pattern allows
    /// '@'/'.', but this sandbox account 500s on specific raw email-shaped values it has seen
    /// before (observed against PAYPAL_CLIENT_ID's sandbox during this build), so email-shaped
    /// buyer ids (eShop usernames are emails) are replaced with a safe, still-traceable form.
    /// </summary>
    private static string? SanitizeMerchantCustomerId(string? merchantCustomerId)
    {
        if (string.IsNullOrEmpty(merchantCustomerId))
        {
            return merchantCustomerId;
        }
        var sanitized = Regex.Replace(merchantCustomerId, "[^0-9a-zA-Z_]", "_");
        return $"eshop_{sanitized}";
    }

    private static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> ChunkRange(DateTimeOffset from, DateTimeOffset to)
    {
        if (from >= to)
        {
            yield return (from, to);
            yield break;
        }

        var chunkSize = TimeSpan.FromDays(31);
        var start = from;
        while (start < to)
        {
            var end = start + chunkSize;
            if (end > to)
            {
                end = to;
            }
            yield return (start, end);
            start = end;
        }
    }

    private static string FormatAmount(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static decimal? ParseAmount(MoneyDto? money)
    {
        if (money is null || string.IsNullOrEmpty(money.Value))
        {
            return null;
        }
        return decimal.Parse(money.Value, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    private static string FormatRfc3339(DateTimeOffset value) => value.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_tokenCache.IsValid)
        {
            return _tokenCache.Token!;
        }

        await _tokenCache.Lock.WaitAsync(cancellationToken);
        try
        {
            if (_tokenCache.IsValid)
            {
                return _tokenCache.Token!;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
            var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
            request.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials") });

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayPal OAuth token request failed with status {0}.", (int)response.StatusCode);
                throw new PayPalApiException(response.StatusCode, "invalid_client",
                    "Failed to acquire a PayPal access token. Check PayPal:ClientId / PayPal:ClientSecret.", null);
            }

            var token = JsonSerializer.Deserialize<OAuthTokenResponseDto>(responseBody, JsonOptions)
                ?? throw new PayPalApiException(response.StatusCode, "invalid_response", "PayPal token response could not be parsed.", null);

            _tokenCache.Token = token.AccessToken;
            _tokenCache.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(token.ExpiresIn - 60, 30));
            return _tokenCache.Token;
        }
        finally
        {
            _tokenCache.Lock.Release();
        }
    }

    private async Task<TResponse> SendAsync<TResponse>(HttpMethod method, string path, object? body, string? requestId, bool preferRepresentation, CancellationToken cancellationToken)
    {
        var responseBody = await SendCoreAsync(method, path, body, requestId, preferRepresentation, cancellationToken);
        return JsonSerializer.Deserialize<TResponse>(responseBody, JsonOptions)
            ?? throw new PayPalApiException(System.Net.HttpStatusCode.InternalServerError, "invalid_response", $"PayPal response for {method} {path} could not be parsed.", null);
    }

    private async Task SendNoContentAsync(HttpMethod method, string path, object? body, string? requestId, CancellationToken cancellationToken)
    {
        await SendCoreAsync(method, path, body, requestId, preferRepresentation: false, cancellationToken);
    }

    private async Task<string> SendCoreAsync(HttpMethod method, string path, object? body, string? requestId, bool preferRepresentation, CancellationToken cancellationToken)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (preferRepresentation)
        {
            request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        }
        if (!string.IsNullOrEmpty(requestId))
        {
            request.Headers.TryAddWithoutValidation("PayPal-Request-Id", requestId);
        }
        if (method != HttpMethod.Get && method != HttpMethod.Delete)
        {
            // PayPal expects application/json even on a body-less POST (e.g. authorize, void);
            // omitting Content-Type entirely gets rejected as UNSUPPORTED_MEDIA_TYPE.
            var json = body is not null ? JsonSerializer.Serialize(body, body.GetType(), JsonOptions) : "{}";
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("PayPal call {0} {1} failed with status {2}.", method, path, (int)response.StatusCode);
            throw BuildException(response.StatusCode, responseBody);
        }

        return responseBody;
    }

    private static PayPalApiException BuildException(System.Net.HttpStatusCode statusCode, string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return new PayPalApiException(statusCode, null, $"PayPal returned HTTP {(int)statusCode} with no body.", null);
        }

        try
        {
            var error = JsonSerializer.Deserialize<ErrorResponseDto>(responseBody, JsonOptions);
            if (error is not null)
            {
                var details = error.Details?
                    .Select(d => d.Issue ?? d.Description ?? string.Empty)
                    .Where(d => !string.IsNullOrEmpty(d))
                    .ToList() ?? new List<string>();

                var message = error.Message ?? $"PayPal returned HTTP {(int)statusCode}.";
                if (details.Count > 0)
                {
                    message += " Details: " + string.Join("; ", details);
                }
                return new PayPalApiException(statusCode, error.Name, message, error.DebugId, details);
            }
        }
        catch (JsonException)
        {
            // fall through to the generic exception below
        }

        return new PayPalApiException(statusCode, null, $"PayPal returned HTTP {(int)statusCode}.", null);
    }
}
