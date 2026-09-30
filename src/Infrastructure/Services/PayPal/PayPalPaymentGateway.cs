using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;
using PayPalServerSdk;
using PayPalServerSdk.Core.ErrorResponse;
using PayPalServerSdk.Core.Exceptions;
using PayPalServerSdk.Errors;
using PayPalServerSdk.Models;
using PayPalServerSdk.Models.Enums;

namespace Microsoft.eShopWeb.Infrastructure.Services.PayPal;

/// <summary>
/// The single place the PayPal .NET SDK is used. Maps ApplicationCore DTOs to SDK request models and SDK
/// responses back to DTOs, and translates every SDK failure into an ApplicationCore domain exception so no
/// SDK type escapes this boundary. Card PAN/CVV are read only to build the request and never logged.
/// </summary>
public class PayPalPaymentGateway : IPayPalPaymentGateway
{
    private readonly PayPalServerSdkClient _client;
    private readonly IAppLogger<PayPalPaymentGateway> _logger;

    // Ask PayPal for the full representation so nested ids and fee/net breakdowns are present on write responses.
    private const string Representation = "return=representation";

    public PayPalPaymentGateway(PayPalServerSdkClient client, IAppLogger<PayPalPaymentGateway> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<AuthorizationResult> AuthorizeWithCardAsync(
        string idempotencyKey, int orderId, decimal amount, string currency, CardDetails card, CancellationToken ct)
    {
        var body = BuildAuthorizeRequest(orderId, amount, currency, new CardRequest
        {
            Number = card.Number,
            Expiry = FormatExpiry(card.ExpiryMonth, card.ExpiryYear),
            SecurityCode = card.SecurityCode,
            Name = card.CardholderName,
            BillingAddress = BuildAddress(card)
        });
        return await CreateOrderAsync(body, idempotencyKey, "authorize", ct);
    }

    public async Task<AuthorizationResult> AuthorizeWithVaultedCardAsync(
        string idempotencyKey, int orderId, decimal amount, string currency, string vaultId, CancellationToken ct)
    {
        var body = BuildAuthorizeRequest(orderId, amount, currency, new CardRequest
        {
            VaultId = vaultId
        });
        return await CreateOrderAsync(body, idempotencyKey, "authorize with saved card", ct);
    }

    private async Task<AuthorizationResult> CreateOrderAsync(
        OrderRequest body, string idempotencyKey, string operation, CancellationToken ct)
    {
        try
        {
            var order = await _client.Orders.CreateOrder(
                payPalMockResponse: null,
                payPalRequestId: idempotencyKey,
                payPalPartnerAttributionId: null,
                payPalClientMetadataId: null,
                payPalAuthAssertion: null,
                body: body,
                prefer: Representation,
                requestOptions: null,
                ct: ct);
            return MapAuthorizationFromOrder(order);
        }
        catch (SdkException<CreateOrderError> ex) { throw TranslateOrderError(operation, ex); }
        catch (Exception ex) { throw TranslateInfrastructure(operation, ex, ct); }
    }

    public async Task<AuthorizationResult> ReauthorizeAsync(
        string idempotencyKey, string authorizationId, decimal amount, string currency, CancellationToken ct)
    {
        try
        {
            var body = new ReauthorizeRequest { Amount = BuildMoney(amount, currency) };
            var auth = await _client.Payments.ReauthorizePayment(
                authorizationId: authorizationId,
                payPalRequestId: idempotencyKey,
                payPalAuthAssertion: null,
                body: body,
                prefer: Representation,
                requestOptions: null,
                ct: ct);
            return MapAuthorizationFromPayment(auth);
        }
        catch (SdkException<ReauthorizePaymentError> ex) { throw TranslatePaymentError("reauthorize", ex.Error); }
        catch (Exception ex) { throw TranslateInfrastructure("reauthorize", ex, ct); }
    }

    public async Task<AuthorizationResult> GetAuthorizationAsync(string authorizationId, CancellationToken ct)
    {
        try
        {
            var auth = await _client.Payments.GetAuthorizedPayment(
                authorizationId: authorizationId,
                payPalMockResponse: null,
                payPalAuthAssertion: null,
                requestOptions: null,
                ct: ct);
            return MapAuthorizationFromPayment(auth);
        }
        catch (SdkException<GetAuthorizedPaymentError> ex) { throw TranslatePaymentError("get authorization", ex.Error); }
        catch (Exception ex) { throw TranslateInfrastructure("get authorization", ex, ct); }
    }

    public async Task VoidAuthorizationAsync(string idempotencyKey, string authorizationId, CancellationToken ct)
    {
        try
        {
            // NOTE: VoidPayment's parameter order puts payPalAuthAssertion BEFORE payPalRequestId.
            await _client.Payments.VoidPayment(
                authorizationId: authorizationId,
                payPalMockResponse: null,
                payPalAuthAssertion: null,
                payPalRequestId: idempotencyKey,
                prefer: "return=minimal",
                requestOptions: null,
                ct: ct);
        }
        catch (SdkException<VoidPaymentError> ex) { throw TranslatePaymentError("void authorization", ex.Error); }
        catch (JsonException)
        {
            // A successful void returns 204 No Content, which the SDK cannot deserialize into
            // PaymentAuthorization. A genuine void rejection surfaces as SdkException<VoidPaymentError>, so a
            // JSON parse failure here means the void was accepted with no body — treat it as success.
            _logger.LogInformation("Void for authorization {0} returned no body (treated as success).", authorizationId);
        }
        catch (Exception ex) { throw TranslateInfrastructure("void authorization", ex, ct); }
    }

    public async Task<CaptureResult> CaptureAuthorizationAsync(string idempotencyKey, string authorizationId, CancellationToken ct)
    {
        try
        {
            var captured = await _client.Payments.CaptureAuthorizedPayment(
                authorizationId: authorizationId,
                payPalMockResponse: null,
                payPalRequestId: idempotencyKey,
                payPalAuthAssertion: null,
                body: null,
                prefer: Representation,
                requestOptions: null,
                ct: ct);

            var result = MapCapture(captured);
            // If the fee/net breakdown was absent (e.g. a still-settling capture), re-fetch once to fill it in.
            if (result.PayPalFeeAmount is null || result.NetAmount is null)
            {
                var refreshed = await _client.Payments.GetCapturedPayment(
                    captureId: result.PayPalCaptureId, payPalMockResponse: null, requestOptions: null, ct: ct);
                var refreshedResult = MapCapture(refreshed);
                if (refreshedResult.PayPalFeeAmount is not null && refreshedResult.NetAmount is not null)
                {
                    return refreshedResult;
                }
            }
            return result;
        }
        catch (SdkException<CaptureAuthorizedPaymentError> ex) { throw TranslatePaymentError("capture", ex.Error); }
        catch (SdkException<GetCapturedPaymentError> ex) { throw TranslatePaymentError("capture", ex.Error); }
        catch (Exception ex) { throw TranslateInfrastructure("capture", ex, ct); }
    }

    public async Task<CaptureResult> GetCaptureAsync(string captureId, CancellationToken ct)
    {
        try
        {
            var captured = await _client.Payments.GetCapturedPayment(
                captureId: captureId, payPalMockResponse: null, requestOptions: null, ct: ct);
            return MapCapture(captured);
        }
        catch (SdkException<GetCapturedPaymentError> ex) { throw TranslatePaymentError("get capture", ex.Error); }
        catch (Exception ex) { throw TranslateInfrastructure("get capture", ex, ct); }
    }

    public async Task<RefundResult> RefundCaptureAsync(
        string idempotencyKey, string captureId, decimal? amount, string currency, CancellationToken ct)
    {
        try
        {
            RefundRequest? body = amount.HasValue
                ? new RefundRequest { Amount = BuildMoney(amount.Value, currency) }
                : null;
            var refund = await _client.Payments.RefundCapturedPayment(
                captureId: captureId,
                payPalMockResponse: null,
                payPalRequestId: idempotencyKey,
                payPalAuthAssertion: null,
                body: body,
                prefer: Representation,
                requestOptions: null,
                ct: ct);
            return MapRefund(refund);
        }
        catch (SdkException<RefundCapturedPaymentError> ex) { throw TranslatePaymentError("refund", ex.Error); }
        catch (Exception ex) { throw TranslateInfrastructure("refund", ex, ct); }
    }

    public async Task<RefundResult> GetRefundAsync(string refundId, CancellationToken ct)
    {
        try
        {
            var refund = await _client.Payments.GetRefund(
                refundId: refundId, payPalMockResponse: null, payPalAuthAssertion: null, requestOptions: null, ct: ct);
            return MapRefund(refund);
        }
        catch (SdkException<GetRefundError> ex) { throw TranslatePaymentError("get refund", ex.Error); }
        catch (Exception ex) { throw TranslateInfrastructure("get refund", ex, ct); }
    }

    public async Task<VaultedCardResult> CreateVaultedCardAsync(
        string idempotencyKey, string buyerId, CardDetails card, CancellationToken ct)
    {
        // Two-step vault: create a setup token from the raw card, then exchange it for a payment (vault) token.
        // The direct inline-PAN CreatePaymentToken path returns an opaque 500 on this account; the raw-PAN
        // flow is modeled on the setup-token side (per the SDK contract), so use that.
        var setupTokenId = await CreateSetupTokenAsync(idempotencyKey, buyerId, card, ct);

        try
        {
            // No Customer object: passing customer.merchant_customer_id makes the sandbox vault endpoint 500.
            // PayPal auto-creates a customer and returns its id; the vault token is charged by its own id.
            var body = new PaymentTokenRequest
            {
                PaymentSource = new PaymentTokenRequestPaymentSource
                {
                    Token = new VaultTokenRequest
                    {
                        Id = setupTokenId,
                        Type = VaultTokenRequestType.SetupToken
                    }
                }
            };
            var response = await _client.Vault.CreatePaymentToken(
                payPalRequestId: Guid.NewGuid().ToString("N"), body: body, requestOptions: null, ct: ct);

            var vaultId = response.Id;
            if (string.IsNullOrEmpty(vaultId))
            {
                throw new PaymentGatewayException("PayPal returned a vault token without an id.");
            }
            var cardMeta = response.PaymentSource?.Card;
            return new VaultedCardResult(vaultId, cardMeta?.Brand?.Value, cardMeta?.LastDigits, cardMeta?.Expiry);
        }
        catch (SdkException<CreatePaymentTokenError> ex) { throw TranslateVaultError("save card", ex.Error); }
        catch (Exception ex) when (ex is not PaymentGatewayException and not PaymentDeclinedException)
        {
            throw TranslateInfrastructure("save card", ex, ct);
        }
    }

    private async Task<string> CreateSetupTokenAsync(string idempotencyKey, string buyerId, CardDetails card, CancellationToken ct)
    {
        try
        {
            // No Customer object: passing customer.merchant_customer_id makes the sandbox vault endpoint 500.
            var body = new SetupTokenRequest
            {
                PaymentSource = new SetupTokenRequestPaymentSource
                {
                    // No ExperienceContext/return-url: this integration is deliberately no-browser.
                    Card = new SetupTokenRequestCard
                    {
                        Number = card.Number,
                        Expiry = FormatExpiry(card.ExpiryMonth, card.ExpiryYear),
                        SecurityCode = card.SecurityCode,
                        Name = card.CardholderName,
                        BillingAddress = BuildAddress(card)
                    }
                }
            };
            var setup = await _client.Vault.CreateSetupToken(
                payPalRequestId: idempotencyKey, body: body, requestOptions: null, ct: ct);

            if (setup.Status == PaymentTokenStatus.PayerActionRequired)
            {
                // A browser approval would be needed to vault this card; stop rather than build a redirect.
                throw new PaymentGatewayException("Saving this card requires browser approval, which this integration does not support.");
            }
            return setup.Id ?? throw new PaymentGatewayException("PayPal returned a setup token without an id.");
        }
        catch (SdkException<CreateSetupTokenError> ex) { throw TranslateVaultError("save card (setup token)", ex.Error); }
        catch (Exception ex) when (ex is not PaymentGatewayException and not PaymentDeclinedException)
        {
            throw TranslateInfrastructure("save card (setup token)", ex, ct);
        }
    }

    public async Task DeleteVaultedCardAsync(string vaultId, CancellationToken ct)
    {
        try
        {
            await _client.Vault.DeletePaymentToken(id: vaultId, requestOptions: null, ct: ct);
        }
        catch (SdkException<DeletePaymentTokenError> ex) { throw TranslateVaultDeleteError("remove card", ex.Error); }
        catch (Exception ex) { throw TranslateInfrastructure("remove card", ex, ct); }
    }

    public async Task<IReadOnlyList<PayPalTransactionRecord>> SearchTransactionsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var records = new List<PayPalTransactionRecord>();
        try
        {
            int page = 1;
            int totalPages;
            do
            {
                var response = await _client.TransactionSearch.SearchTransactions(
                    startDate: FormatSearchDate(from),
                    endDate: FormatSearchDate(to),
                    transactionId: null,
                    transactionType: null,
                    transactionStatus: null,
                    transactionAmount: null,
                    transactionCurrency: null,
                    paymentInstrumentType: null,
                    storeId: null,
                    terminalId: null,
                    fields: "transaction_info",
                    balanceAffectingRecordsOnly: "Y",
                    pageSize: 100,
                    page: page,
                    requestOptions: null,
                    ct: ct);

                totalPages = response.TotalPages ?? 1;
                foreach (var detail in response.TransactionDetails ?? Enumerable.Empty<TransactionDetails>())
                {
                    var info = detail.TransactionInfo;
                    if (info?.TransactionId is null) continue;
                    records.Add(new PayPalTransactionRecord(
                        info.TransactionId,
                        ParseMoney(info.TransactionAmount),
                        info.TransactionAmount?.CurrencyCode,
                        info.TransactionStatus,
                        ParseSearchDate(info.TransactionInitiationDate),
                        info.InvoiceId,
                        info.CustomField));
                }
                page++;
            }
            while (page <= totalPages);

            return records;
        }
        catch (SdkException<RawError> ex)
        {
            // SearchTransactions is the SDK's only Case-B operation: RawError carries the real status.
            var status = (int)ex.Error.StatusCode;
            _logger.LogWarning("PayPal transaction search failed: status={0} body={1}", status, Truncate(ex.Error.ReadAsString()));
            throw new PaymentGatewayException($"PayPal transaction search failed (status {status}).", ex);
        }
        catch (Exception ex) { throw TranslateInfrastructure("transaction search", ex, ct); }
    }

    // --- request builders ------------------------------------------------------------------------

    private static OrderRequest BuildAuthorizeRequest(int orderId, decimal amount, string currency, CardRequest card)
    {
        return new OrderRequest
        {
            Intent = CheckoutPaymentIntent.Authorize,
            PurchaseUnits = new List<PurchaseUnitRequest>
            {
                new PurchaseUnitRequest
                {
                    Amount = new AmountWithBreakdown
                    {
                        CurrencyCode = currency,
                        Value = FormatAmount(amount)
                    },
                    // InvoiceId must be globally unique per transaction (the merchant account enforces this),
                    // so make it unique per authorize attempt. CustomId carries the eShop order id for
                    // reconciliation correlation and does not need to be unique.
                    InvoiceId = $"eshop-order-{orderId}-{Guid.NewGuid():N}",
                    CustomId = orderId.ToString(CultureInfo.InvariantCulture)
                }
            },
            PaymentSource = new PaymentSource { Card = card }
        };
    }

    private static Address BuildAddress(CardDetails card) => new Address
    {
        AddressLine1 = card.BillingAddressLine1,
        AddressLine2 = card.BillingAddressLine2,
        AdminArea2 = card.City,
        AdminArea1 = card.State,
        PostalCode = card.PostalCode,
        CountryCode = card.CountryCode
    };

    private static Money BuildMoney(decimal amount, string currency) =>
        new Money { CurrencyCode = currency, Value = FormatAmount(amount) };

    // --- response mappers ------------------------------------------------------------------------

    private AuthorizationResult MapAuthorizationFromOrder(Order order)
    {
        var statusRaw = order.Status?.Value ?? string.Empty;
        var requiresPayerAction = order.Status == OrderStatus.PayerActionRequired;

        var authorization = order.PurchaseUnits?
            .SelectMany(pu => pu.Payments?.Authorizations ?? Enumerable.Empty<AuthorizationWithAdditionalData>())
            .FirstOrDefault();

        if (requiresPayerAction || authorization is null)
        {
            if (requiresPayerAction)
            {
                // Signal the caller to stop; no authorization id exists in this outcome.
                return new AuthorizationResult(order.Id ?? string.Empty, string.Empty, statusRaw, 0m, default, true);
            }
            throw new PaymentGatewayException(
                $"PayPal authorized order response contained no authorization (status {statusRaw}).");
        }

        return new AuthorizationResult(
            order.Id ?? string.Empty,
            authorization.Id ?? throw new PaymentGatewayException("PayPal authorization had no id."),
            authorization.Status?.Value ?? statusRaw,
            ParseMoney(authorization.Amount) ?? 0m,
            ParseExpiry(authorization.ExpirationTime),
            false);
    }

    private static AuthorizationResult MapAuthorizationFromPayment(PaymentAuthorization auth) =>
        new AuthorizationResult(
            string.Empty,
            auth.Id ?? throw new PaymentGatewayException("PayPal authorization had no id."),
            auth.Status?.Value ?? string.Empty,
            ParseMoney(auth.Amount) ?? 0m,
            ParseExpiry(auth.ExpirationTime),
            false);

    private static CaptureResult MapCapture(CapturedPayment captured)
    {
        var breakdown = captured.SellerReceivableBreakdown;
        return new CaptureResult(
            captured.Id ?? throw new PaymentGatewayException("PayPal capture had no id."),
            captured.Status?.Value ?? string.Empty,
            ParseMoney(captured.Amount) ?? 0m,
            ParseMoney(breakdown?.PaypalFee),
            ParseMoney(breakdown?.NetAmount));
    }

    private static RefundResult MapRefund(Refund refund) =>
        new RefundResult(
            refund.Id ?? throw new PaymentGatewayException("PayPal refund had no id."),
            refund.Status?.Value ?? string.Empty,
            ParseMoney(refund.Amount) ?? 0m,
            ParseMoney(refund.SellerPayableBreakdown?.TotalRefundedAmount));

    // --- error translation -----------------------------------------------------------------------

    private Exception TranslateOrderError(string operation, SdkException<CreateOrderError> ex)
    {
        if (ex.Error.TryGetError(out var e))
        {
            return TypedRejection(operation, e.Name, Describe(e));
        }
        if (ex.Error.TryGetRawError(out var raw))
        {
            return FromStatus(operation, (int)raw.StatusCode, SafeRead(raw));
        }
        return new PaymentGatewayException($"PayPal {operation} failed with an unrecognised error.", ex);
    }

    // Orders/Payments Case-A errors all expose TryGetError(out Error) + TryGetRawError. They are distinct C#
    // types, so each catch passes its already-extracted values here.
    private Exception TranslatePaymentError(string operation, CaptureAuthorizedPaymentError error) => FromError(operation, error.TryGetError, error.TryGetRawError);
    private Exception TranslatePaymentError(string operation, RefundCapturedPaymentError error) => FromError(operation, error.TryGetError, error.TryGetRawError);
    private Exception TranslatePaymentError(string operation, ReauthorizePaymentError error) => FromError(operation, error.TryGetError, error.TryGetRawError);
    private Exception TranslatePaymentError(string operation, VoidPaymentError error) => FromError(operation, error.TryGetError, error.TryGetRawError);
    private Exception TranslatePaymentError(string operation, GetAuthorizedPaymentError error) => FromError(operation, error.TryGetError, error.TryGetRawError);
    private Exception TranslatePaymentError(string operation, GetCapturedPaymentError error) => FromError(operation, error.TryGetError, error.TryGetRawError);
    private Exception TranslatePaymentError(string operation, GetRefundError error) => FromError(operation, error.TryGetError, error.TryGetRawError);

    private Exception TranslateVaultError(string operation, CreatePaymentTokenError error) => FromError1(operation, error.TryGetError1, error.TryGetRawError);
    private Exception TranslateVaultError(string operation, CreateSetupTokenError error) => FromError1(operation, error.TryGetError1, error.TryGetRawError);
    private Exception TranslateVaultDeleteError(string operation, DeletePaymentTokenError error) => FromError1(operation, error.TryGetError1, error.TryGetRawError);

    private delegate bool TryGetError(out Error value);
    private delegate bool TryGetError1(out Error1 value);
    private delegate bool TryGetRaw(out PayPalServerSdk.Core.ErrorResponse.RawError value);

    private Exception FromError(string operation, TryGetError tryGetError, TryGetRaw tryGetRaw)
    {
        if (tryGetError(out var e))
        {
            return TypedRejection(operation, e.Name, Describe(e));
        }
        if (tryGetRaw(out var raw))
        {
            return FromStatus(operation, (int)raw.StatusCode, SafeRead(raw));
        }
        return new PaymentGatewayException($"PayPal {operation} failed with an unrecognised error.");
    }

    private Exception FromError1(string operation, TryGetError1 tryGetError1, TryGetRaw tryGetRaw)
    {
        if (tryGetError1(out var e))
        {
            return TypedRejection(operation, e.Name, Describe(e));
        }
        if (tryGetRaw(out var raw))
        {
            return FromStatus(operation, (int)raw.StatusCode, SafeRead(raw));
        }
        return new PaymentGatewayException($"PayPal {operation} failed with an unrecognised error.");
    }

    // A typed Case-A error is only produced for the operation's enumerated 4xx statuses (R3), so it is always
    // a deterministic client rejection the caller can act on -> 402. The exact PayPal issue is logged, never
    // surfaced with card data.
    private Exception TypedRejection(string operation, string? name, string detail)
    {
        _logger.LogWarning("PayPal {0} rejected: name={1} detail={2}", operation, name ?? "(none)", Truncate(detail));
        return new PaymentDeclinedException(
            $"PayPal declined the {operation}{(name is null ? "." : $" ({name}).")}", name);
    }

    private Exception FromStatus(string operation, int statusCode, string detail)
    {
        _logger.LogWarning("PayPal {0} failed: status={1} detail={2}", operation, statusCode, Truncate(detail));
        if (statusCode is >= 400 and < 500)
        {
            return new PaymentDeclinedException($"PayPal declined the {operation} (status {statusCode}).");
        }
        return new PaymentGatewayException($"PayPal could not complete the {operation} (status {statusCode}).");
    }

    private static string Describe(Error e)
    {
        var details = e.Details is null
            ? string.Empty
            : string.Join("; ", e.Details.Select(d => $"{d.Issue}:{d.Description}"));
        return string.IsNullOrEmpty(details) ? e.Message : $"{e.Message} [{details}]";
    }

    private static string Describe(Error1 e)
    {
        var details = e.Details is null
            ? string.Empty
            : string.Join("; ", e.Details.Select(d => $"{d.Issue}:{d.Description}"));
        return string.IsNullOrEmpty(details) ? e.Message : $"{e.Message} [{details}]";
    }

    private Exception TranslateInfrastructure(string operation, Exception ex, CancellationToken ct)
    {
        if (ex is OperationCanceledException && ct.IsCancellationRequested)
        {
            return ex; // caller cancelled — surface as cancellation, not a gateway fault
        }
        if (ex is JsonException)
        {
            // JsonException reaches here from two directions (see contract sheet). Disambiguate with the real
            // status captured by the diagnostics handler: a 4xx is a rejection, not an outage.
            var status = (int?)PayPalRequestContext.LastStatusCode;
            _logger.LogWarning("PayPal {0} response could not be parsed (status={1}).",
                operation, status?.ToString(CultureInfo.InvariantCulture) ?? "unknown");
            if (status is >= 400 and < 500)
            {
                return new PaymentDeclinedException($"PayPal declined the {operation}.", null, ex);
            }
            return new PaymentGatewayException(
                $"PayPal returned a response for the {operation} that could not be processed.", ex);
        }
        if (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException)
        {
            return new PaymentGatewayException($"PayPal was unreachable during the {operation}.", ex);
        }
        _logger.LogWarning("Unexpected error during PayPal {0}: {1}", operation, ex.Message);
        return new PaymentGatewayException($"Unexpected error during the {operation}.", ex);
    }

    // --- formatting / parsing --------------------------------------------------------------------

    private static string FormatAmount(decimal amount) =>
        amount.ToString("F2", CultureInfo.InvariantCulture);

    private static string FormatExpiry(int month, int year)
    {
        if (year < 100) year += 2000;
        return $"{year:D4}-{month:D2}";
    }

    private static string FormatSearchDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static decimal? ParseMoney(Money? money)
    {
        if (money?.Value is null) return null;
        return decimal.TryParse(money.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static DateTimeOffset ParseExpiry(string? expiration)
    {
        if (!string.IsNullOrWhiteSpace(expiration) &&
            DateTimeOffset.TryParse(expiration, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value))
        {
            return value;
        }
        // No expiry reported: treat the hold as already stale so fulfilment renews it rather than assuming validity.
        return DateTimeOffset.UtcNow;
    }

    private static DateTimeOffset? ParseSearchDate(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }
        return null;
    }

    private static string SafeRead(PayPalServerSdk.Core.ErrorResponse.RawError raw)
    {
        try { return Truncate(raw.ReadAsString()); }
        catch { return "(unreadable error body)"; }
    }

    private static string Truncate(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= 500 ? text : text.Substring(0, 500);
    }
}

