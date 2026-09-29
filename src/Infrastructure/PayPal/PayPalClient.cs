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
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.Infrastructure.PayPal.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// The single seam to PayPal's REST API. Resolves the token, sets the mandated idempotency
/// (<c>PayPal-Request-Id</c>) and <c>Prefer: return=representation</c> headers, maps wire shapes to
/// the domain-facing contracts, and turns a payer-action/challenge into a distinct loud exception.
/// No card data is ever logged.
/// </summary>
public class PayPalClient : IPayPalClient
{
    private readonly HttpClient _httpClient;
    private readonly IPayPalTokenProvider _tokenProvider;
    private readonly ILogger<PayPalClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // Transaction Search: documented maximum window is 31 days; page_size max is 500.
    private const int MaxSearchWindowDays = 31;
    private const int SearchPageSize = 500;

    public PayPalClient(HttpClient httpClient, IPayPalTokenProvider tokenProvider, ILogger<PayPalClient> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    // ---- Orders v2: authorize ----

    public Task<AuthorizationResult> AuthorizeWithCardAsync(int orderId, string currencyCode, decimal amount,
        CardDetails card, CancellationToken cancellationToken = default)
    {
        var source = new PayPalPaymentSourceRequest
        {
            Card = new PayPalCardRequest
            {
                Name = card.Name,
                Number = card.Number,
                Expiry = card.Expiry,
                SecurityCode = card.SecurityCode,
                BillingAddress = MapBillingAddress(card.BillingAddress),
            }
        };
        return CreateAuthorizedOrderAsync(orderId, currencyCode, amount, source, cancellationToken);
    }

    public Task<AuthorizationResult> AuthorizeWithVaultedCardAsync(int orderId, string currencyCode, decimal amount,
        string vaultId, CancellationToken cancellationToken = default)
    {
        var source = new PayPalPaymentSourceRequest
        {
            Card = new PayPalCardRequest { VaultId = vaultId }
        };
        return CreateAuthorizedOrderAsync(orderId, currencyCode, amount, source, cancellationToken);
    }

    private async Task<AuthorizationResult> CreateAuthorizedOrderAsync(int orderId, string currencyCode,
        decimal amount, PayPalPaymentSourceRequest source, CancellationToken cancellationToken)
    {
        var request = new PayPalCreateOrderRequest
        {
            Intent = "AUTHORIZE",
            PurchaseUnits = new List<PayPalPurchaseUnitRequest>
            {
                new()
                {
                    CustomId = orderId.ToString(CultureInfo.InvariantCulture),
                    Amount = Money(currencyCode, amount),
                }
            },
            PaymentSource = source,
        };

        var order = await SendAsync<PayPalOrderResponse>(HttpMethod.Post, "/v2/checkout/orders", request,
            requestId: $"paypal-auth-{orderId}", prefer: "return=representation", context: "order authorization",
            cancellationToken: cancellationToken);

        GuardNoPayerAction(order!.Status, order.Links, "order authorization");

        var authorization = order.PurchaseUnits?
            .Select(pu => pu.Payments?.Authorizations?.FirstOrDefault())
            .FirstOrDefault(a => a is not null);

        // Single-step card orders return the authorization inline. Defensive fallback: if the order
        // came back APPROVED with no authorization populated, complete it via the authorize endpoint.
        if (authorization is null && string.Equals(order.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
        {
            var authorized = await SendAsync<PayPalOrderResponse>(HttpMethod.Post,
                $"/v2/checkout/orders/{order.Id}/authorize", new { },
                requestId: $"paypal-auth-{orderId}-confirm", prefer: "return=representation",
                context: "order authorization", cancellationToken: cancellationToken);

            GuardNoPayerAction(authorized!.Status, authorized.Links, "order authorization");
            authorization = authorized.PurchaseUnits?
                .Select(pu => pu.Payments?.Authorizations?.FirstOrDefault())
                .FirstOrDefault(a => a is not null);
        }

        if (authorization is null || string.IsNullOrEmpty(authorization.Id))
            throw new PayPalApiException(502, "no_authorization",
                "PayPal did not return an authorization for the order.", null, order.Status);

        return new AuthorizationResult(
            order.Id ?? "",
            authorization.Id,
            authorization.Status ?? "",
            authorization.ExpirationTime ?? DateTimeOffset.UtcNow);
    }

    // ---- Payments v2 ----

    public async Task<AuthorizationDetails> GetAuthorizationAsync(string authorizationId,
        CancellationToken cancellationToken = default)
    {
        var auth = await SendAsync<PayPalAuthorization>(HttpMethod.Get,
            $"/v2/payments/authorizations/{authorizationId}", body: null,
            requestId: null, prefer: null, context: "authorization lookup", cancellationToken: cancellationToken);

        return new AuthorizationDetails(
            auth!.Id ?? authorizationId,
            auth.Status ?? "",
            auth.ExpirationTime,
            auth.StatusDetails?.Reason);
    }

    public async Task<ReauthorizationResult> ReauthorizeAsync(string authorizationId, string currencyCode,
        decimal amount, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var request = new PayPalReauthorizeRequest { Amount = Money(currencyCode, amount) };
        var auth = await SendAsync<PayPalAuthorization>(HttpMethod.Post,
            $"/v2/payments/authorizations/{authorizationId}/reauthorize", request,
            requestId: idempotencyKey, prefer: "return=representation", context: "reauthorization",
            cancellationToken: cancellationToken);

        return new ReauthorizationResult(
            auth!.Id ?? authorizationId,
            auth.Status ?? "",
            auth.ExpirationTime ?? DateTimeOffset.UtcNow);
    }

    public async Task<CaptureResult> CaptureAsync(string authorizationId, string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = new PayPalCaptureRequest { FinalCapture = true };
        var capture = await SendAsync<PayPalCapture>(HttpMethod.Post,
            $"/v2/payments/authorizations/{authorizationId}/capture", request,
            requestId: idempotencyKey, prefer: "return=representation", context: "capture",
            cancellationToken: cancellationToken);

        var breakdown = capture!.SellerReceivableBreakdown;
        return new CaptureResult(
            capture.Id ?? "",
            capture.Status ?? "",
            PayPalMoneyFormatter.Parse(capture.Amount?.Value ?? breakdown?.GrossAmount?.Value),
            breakdown?.PayPalFee is null ? null : PayPalMoneyFormatter.Parse(breakdown.PayPalFee.Value),
            breakdown?.NetAmount is null ? null : PayPalMoneyFormatter.Parse(breakdown.NetAmount.Value));
    }

    public async Task VoidAsync(string authorizationId, string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        // Void returns 204 with no body under the default Prefer; treat any 2xx as success.
        await SendRawAsync(HttpMethod.Post, $"/v2/payments/authorizations/{authorizationId}/void",
            body: null, requestId: idempotencyKey, prefer: null, context: "void",
            cancellationToken: cancellationToken);
    }

    public async Task<RefundResult> RefundAsync(string captureId, string currencyCode, decimal? amount,
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        // Empty body = refund the full remaining amount; an amount = partial refund.
        object body = amount is null
            ? new { }
            : new PayPalRefundRequest { Amount = Money(currencyCode, amount.Value) };

        var refund = await SendAsync<PayPalRefundResponse>(HttpMethod.Post,
            $"/v2/payments/captures/{captureId}/refund", body,
            requestId: idempotencyKey, prefer: "return=representation", context: "refund",
            cancellationToken: cancellationToken);

        return new RefundResult(
            refund!.Id ?? "",
            refund.Status ?? "",
            PayPalMoneyFormatter.Parse(refund.Amount?.Value));
    }

    // ---- Vault v3 ----

    public async Task<SetupTokenResult> CreateSetupTokenAsync(CardDetails card,
        CancellationToken cancellationToken = default)
    {
        var request = new PayPalSetupTokenRequest
        {
            PaymentSource = new PayPalSetupTokenPaymentSource
            {
                Card = new PayPalVaultCardRequest
                {
                    Name = card.Name,
                    Number = card.Number,
                    Expiry = card.Expiry,
                    SecurityCode = card.SecurityCode,
                    BillingAddress = MapBillingAddress(card.BillingAddress),
                    VerificationMethod = "SCA_WHEN_REQUIRED",
                    // Required by the vault API. Used only if PayPal triggers a browser challenge —
                    // which surfaces as PAYER_ACTION_REQUIRED and is a STOP condition here.
                    ExperienceContext = new PayPalVaultExperienceContext
                    {
                        ReturnUrl = "https://example.com/paypal/vault/return",
                        CancelUrl = "https://example.com/paypal/vault/cancel",
                    },
                }
            }
        };

        var token = await SendAsync<PayPalSetupTokenResponse>(HttpMethod.Post, "/v3/vault/setup-tokens", request,
            requestId: Guid.NewGuid().ToString(), prefer: null, context: "vault setup token",
            cancellationToken: cancellationToken);

        GuardNoPayerAction(token!.Status, token.Links, "vault setup token");

        if (string.IsNullOrEmpty(token.Id))
            throw new PayPalApiException(502, "no_setup_token", "PayPal did not return a setup token id.", null, null);

        return new SetupTokenResult(token.Id, token.Status ?? "");
    }

    public async Task<VaultedCard> CreatePaymentTokenAsync(string setupTokenId,
        CancellationToken cancellationToken = default)
    {
        var request = new PayPalPaymentTokenRequest
        {
            PaymentSource = new PayPalPaymentTokenSource
            {
                Token = new PayPalTokenReference { Id = setupTokenId, Type = "SETUP_TOKEN" }
            }
        };

        var token = await SendAsync<PayPalPaymentTokenResponse>(HttpMethod.Post, "/v3/vault/payment-tokens", request,
            requestId: Guid.NewGuid().ToString(), prefer: null, context: "vault payment token",
            cancellationToken: cancellationToken);

        if (string.IsNullOrEmpty(token!.Id))
            throw new PayPalApiException(502, "no_payment_token", "PayPal did not return a payment token id.", null, null);

        var cardInfo = token.PaymentSource?.Card;
        return new VaultedCard(
            token.Id,
            cardInfo?.Brand ?? "",
            cardInfo?.LastDigits ?? "",
            cardInfo?.Expiry ?? "");
    }

    public async Task DeletePaymentTokenAsync(string vaultId, CancellationToken cancellationToken = default)
    {
        await SendRawAsync(HttpMethod.Delete, $"/v3/vault/payment-tokens/{vaultId}", body: null,
            requestId: null, prefer: null, context: "delete vault token", cancellationToken: cancellationToken);
    }

    // ---- Transaction Search v1 ----

    public async Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var results = new List<PayPalTransaction>();

        // Chunk the range into <=31-day windows, and page through every page of each window, so the
        // whole range is covered rather than just the first page.
        var windowStart = from;
        while (windowStart < to)
        {
            var windowEnd = windowStart.AddDays(MaxSearchWindowDays);
            if (windowEnd > to) windowEnd = to;

            var page = 1;
            int totalPages;
            do
            {
                var query = "/v1/reporting/transactions" +
                    $"?start_date={Uri.EscapeDataString(FormatRfc3339(windowStart))}" +
                    $"&end_date={Uri.EscapeDataString(FormatRfc3339(windowEnd))}" +
                    "&fields=all" +
                    $"&page_size={SearchPageSize}" +
                    $"&page={page}";

                var response = await SendAsync<PayPalTransactionSearchResponse>(HttpMethod.Get, query, body: null,
                    requestId: null, prefer: null, context: "transaction search", cancellationToken: cancellationToken);

                if (response!.TransactionDetails is not null)
                {
                    foreach (var detail in response.TransactionDetails)
                    {
                        var info = detail.TransactionInfo;
                        if (info is null) continue;
                        results.Add(new PayPalTransaction(
                            info.TransactionId ?? "",
                            info.CustomField,
                            PayPalMoneyFormatter.Parse(info.TransactionAmount?.Value),
                            info.TransactionAmount?.CurrencyCode ?? "",
                            info.TransactionStatus ?? "",
                            info.TransactionEventCode,
                            info.TransactionInitiationDate));
                    }
                }

                totalPages = response.TotalPages <= 0 ? 1 : response.TotalPages;
                page++;
            }
            while (page <= totalPages);

            windowStart = windowEnd;
        }

        return results;
    }

    // ---- Helpers ----

    private static PayPalMoney Money(string currencyCode, decimal amount) => new()
    {
        CurrencyCode = currencyCode,
        Value = PayPalMoneyFormatter.Format(amount),
    };

    private static PayPalCardBillingAddress? MapBillingAddress(CardBillingAddress? address)
    {
        if (address is null) return null;
        return new PayPalCardBillingAddress
        {
            AddressLine1 = address.AddressLine1,
            AddressLine2 = address.AddressLine2,
            AdminArea2 = address.AdminArea2,
            AdminArea1 = address.AdminArea1,
            PostalCode = address.PostalCode,
            CountryCode = address.CountryCode,
        };
    }

    private static string FormatRfc3339(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>STOP condition: a browser-approval challenge must never be worked around.</summary>
    private static void GuardNoPayerAction(string? status, List<PayPalLink>? links, string context)
    {
        if (string.Equals(status, "PAYER_ACTION_REQUIRED", StringComparison.OrdinalIgnoreCase))
            throw new PayPalPayerActionRequiredException(context);

        if (links is not null && links.Any(l =>
                l.Rel is not null &&
                (l.Rel.Contains("payer-action", StringComparison.OrdinalIgnoreCase) ||
                 l.Rel.Equals("approve", StringComparison.OrdinalIgnoreCase))))
        {
            throw new PayPalPayerActionRequiredException(context);
        }
    }

    private async Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, object? body,
        string? requestId, string? prefer, string context, CancellationToken cancellationToken)
    {
        using var response = await SendRawAsync(method, path, body, requestId, prefer, context, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
            return default;
        return JsonSerializer.Deserialize<TResponse>(content, JsonOptions);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, object? body,
        string? requestId, string? prefer, string context, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!string.IsNullOrEmpty(requestId))
            request.Headers.TryAddWithoutValidation("PayPal-Request-Id", requestId);
        if (!string.IsNullOrEmpty(prefer))
            request.Headers.TryAddWithoutValidation("Prefer", prefer);

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, body.GetType(), JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return response;

        await ThrowApiExceptionAsync(response, context, cancellationToken);
        return response; // unreachable
    }

    private async Task ThrowApiExceptionAsync(HttpResponseMessage response, string context,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        PayPalErrorResponse? error = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(content))
                error = JsonSerializer.Deserialize<PayPalErrorResponse>(content, JsonOptions);
        }
        catch (JsonException)
        {
            // Non-JSON error body; fall through with the raw content as details.
        }

        var details = error?.Details is { Count: > 0 }
            ? string.Join("; ", error.Details.Select(d => $"{d.Issue}:{d.Field}:{d.Description}"))
            : null;

        // Always log debug_id for traceability; never log request bodies (card data).
        _logger.LogError("PayPal {Context} failed: status={Status} name={Name} debug_id={DebugId}",
            context, (int)response.StatusCode, error?.Name, error?.DebugId);

        throw new PayPalApiException(
            (int)response.StatusCode,
            error?.Name,
            error?.Message ?? $"PayPal {context} failed with status {(int)response.StatusCode}.",
            error?.DebugId,
            details);
    }
}
