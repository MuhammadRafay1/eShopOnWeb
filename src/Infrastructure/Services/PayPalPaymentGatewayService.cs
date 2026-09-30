using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using PayPalServerSdk;
using PayPalServerSdk.Core.ErrorResponse;
using PayPalServerSdk.Core.Exceptions;
using PayPalServerSdk.Errors;
using PayPalServerSdk.Models;
using PayPalServerSdk.Models.Enums;
using BillingAddress = Microsoft.eShopWeb.ApplicationCore.Interfaces.BillingAddress;

namespace Microsoft.eShopWeb.Infrastructure.Services;

/// <summary>
/// The only place the PayPal .NET SDK is referenced. Translates every SDK model into the plain
/// records of <see cref="IPaymentGatewayService"/> and every SDK failure into a
/// <see cref="PaymentGatewayException"/> (or <see cref="PaymentAuthorizationNotRenewableException"/>
/// for a reauthorization that PayPal refuses). Card details are only ever passed through — never
/// stored, never logged.
/// </summary>
public class PayPalPaymentGatewayService : IPaymentGatewayService
{
    private const string Representation = "return=representation";

    private readonly PayPalServerSdkClient _client;
    private readonly IAppLogger<PayPalPaymentGatewayService> _logger;

    public PayPalPaymentGatewayService(PayPalServerSdkClient client,
        IAppLogger<PayPalPaymentGatewayService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<AuthorizationResult> AuthorizeAsync(AuthorizationRequest request, CancellationToken ct = default)
    {
        var orderRequest = new OrderRequest
        {
            Intent = CheckoutPaymentIntent.Authorize,
            PurchaseUnits = new List<PurchaseUnitRequest>
            {
                new PurchaseUnitRequest
                {
                    Amount = new AmountWithBreakdown
                    {
                        CurrencyCode = request.Currency,
                        Value = Money2(request.Amount)
                    }
                }
            }
        };

        try
        {
            var created = await _client.Orders.CreateOrder(
                payPalMockResponse: null,
                payPalRequestId: $"create-order:{request.IdempotencyKeyBase}",
                payPalPartnerAttributionId: null,
                payPalClientMetadataId: null,
                payPalAuthAssertion: null,
                body: orderRequest,
                prefer: Representation,
                ct: ct);

            var payPalOrderId = created.Id;
            if (created.Status is not null && created.Status == OrderStatus.PayerActionRequired)
                return PayerAction();

            var card = request.VaultId is not null
                ? new CardRequest { VaultId = request.VaultId }
                : new CardRequest
                {
                    Name = request.Card!.Name,
                    Number = request.Card!.Number,
                    Expiry = request.Card!.Expiry,
                    SecurityCode = request.Card!.SecurityCode,
                    BillingAddress = MapAddress(request.Card!.BillingAddress)
                };

            var authorizeBody = new OrderAuthorizeRequest
            {
                PaymentSource = new OrderAuthorizeRequestPaymentSource { Card = card }
            };

            var authorized = await _client.Orders.AuthorizeOrder(
                id: payPalOrderId,
                payPalMockResponse: null,
                payPalRequestId: $"authorize:{request.IdempotencyKeyBase}",
                payPalClientMetadataId: null,
                payPalAuthAssertion: null,
                body: authorizeBody,
                prefer: Representation,
                ct: ct);

            if (authorized.Status is not null && authorized.Status == OrderStatus.PayerActionRequired)
            {
                _logger.LogWarning("PayPal returned PAYER_ACTION_REQUIRED for order {0}; aborting (no browser approval flow).", payPalOrderId ?? "");
                return PayerAction();
            }

            AuthorizationWithAdditionalData? auth = null;
            var units = authorized.PurchaseUnits;
            if (units is { Count: > 0 })
            {
                var authorizations = units[0].Payments?.Authorizations;
                if (authorizations is { Count: > 0 })
                    auth = authorizations[0];
            }

            if (auth?.Id is null)
                throw new PaymentGatewayException("PayPal did not return an authorization for the order.");

            return new AuthorizationResult(
                PayerActionRequired: false,
                PayPalOrderId: payPalOrderId,
                AuthorizationId: auth.Id,
                Status: auth.Status?.Value,
                ExpiresAt: ParseDate(auth.ExpirationTime));
        }
        catch (SdkException<CreateOrderError> ex)
        {
            throw Gateway("create order",
                ex.Error.TryGetError(out var e) ? e : null,
                ex.Error.TryGetRawError(out var raw) ? raw : null);
        }
        catch (SdkException<AuthorizeOrderError> ex)
        {
            throw Gateway("authorize order",
                ex.Error.TryGetError(out var e) ? e : null,
                ex.Error.TryGetRawError(out var raw) ? raw : null);
        }
        catch (Exception ex) when (IsTransport(ex)) { throw Unreachable(ex); }
        catch (JsonException ex) { throw Unprocessable(ex); }
    }

    public async Task<AuthorizationSnapshot> GetAuthorizationStatusAsync(string authorizationId, CancellationToken ct = default)
    {
        try
        {
            var pa = await _client.Payments.GetAuthorizedPayment(
                authorizationId: authorizationId,
                payPalMockResponse: null,
                payPalAuthAssertion: null,
                ct: ct);

            return new AuthorizationSnapshot(pa.Status?.Value ?? "", ParseDate(pa.ExpirationTime));
        }
        catch (SdkException<GetAuthorizedPaymentError> ex)
        {
            throw Gateway("read authorization",
                ex.Error.TryGetError(out var e) ? e : null,
                ex.Error.TryGetRawError(out var raw) ? raw : null);
        }
        catch (Exception ex) when (IsTransport(ex)) { throw Unreachable(ex); }
        catch (JsonException ex) { throw Unprocessable(ex); }
    }

    public async Task<AuthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string orderIdForIdempotency, CancellationToken ct = default)
    {
        try
        {
            var pa = await _client.Payments.ReauthorizePayment(
                authorizationId: authorizationId,
                payPalRequestId: $"reauthorize:{orderIdForIdempotency}:{authorizationId}",
                payPalAuthAssertion: null,
                body: new ReauthorizeRequest { Amount = new Money { CurrencyCode = currency, Value = Money2(amount) } },
                prefer: Representation,
                ct: ct);

            if (pa.Id is null)
                throw new PaymentAuthorizationNotRenewableException(
                    $"Authorization {authorizationId} could not be renewed: PayPal returned no new authorization.");

            return new AuthorizationResult(false, null, pa.Id, pa.Status?.Value, ParseDate(pa.ExpirationTime));
        }
        catch (SdkException<ReauthorizePaymentError> ex)
        {
            // A typed rejection here means the authorization is not renewable — surface PayPal's own words.
            Error? e = ex.Error.TryGetError(out var er) ? er : null;
            RawError? raw = e is null && ex.Error.TryGetRawError(out var r) ? r : null;
            string detail = e is not null ? $"{e.Name} — {e.Message}"
                : raw is not null ? $"PayPal returned HTTP {(int)raw.StatusCode}."
                : "PayPal declined the reauthorization.";
            string debugSuffix = e?.DebugId is not null ? $" (PayPal debug id {e.DebugId})" : "";
            throw new PaymentAuthorizationNotRenewableException(
                $"Authorization {authorizationId} cannot be renewed: {detail}{debugSuffix}");
        }
        catch (Exception ex) when (IsTransport(ex)) { throw Unreachable(ex); }
        catch (JsonException ex) { throw Unprocessable(ex); }
    }

    public async Task VoidAuthorizationAsync(string authorizationId, string orderIdForIdempotency, CancellationToken ct = default)
    {
        try
        {
            await _client.Payments.VoidPayment(
                authorizationId: authorizationId,
                payPalMockResponse: null,
                payPalAuthAssertion: null,
                payPalRequestId: $"void:{orderIdForIdempotency}:{authorizationId}",
                ct: ct);
        }
        catch (SdkException<VoidPaymentError> ex)
        {
            throw Gateway("void authorization",
                ex.Error.TryGetError(out var e) ? e : null,
                ex.Error.TryGetRawError(out var raw) ? raw : null);
        }
        catch (Exception ex) when (IsTransport(ex)) { throw Unreachable(ex); }
        catch (JsonException)
        {
            // A successful void returns 204 No Content; the SDK (whose VoidPayment is typed to return
            // PaymentAuthorization) can throw parsing the empty body. Rather than assume failure,
            // confirm the authorization actually reached a voided state before reporting an outcome.
            var snapshot = await GetAuthorizationStatusAsync(authorizationId, ct);
            if (!string.Equals(snapshot.Status, "VOIDED", StringComparison.OrdinalIgnoreCase))
                throw new PaymentGatewayException("PayPal returned an unreadable response to the void request.");
        }
    }

    public async Task<CaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency, string orderIdForIdempotency, CancellationToken ct = default)
    {
        try
        {
            var cap = await _client.Payments.CaptureAuthorizedPayment(
                authorizationId: authorizationId,
                payPalMockResponse: null,
                payPalRequestId: $"capture:{orderIdForIdempotency}:{authorizationId}",
                payPalAuthAssertion: null,
                body: new CaptureRequest { FinalCapture = true },
                prefer: Representation,
                ct: ct);

            if (cap.Id is null)
                throw new PaymentGatewayException("PayPal returned a capture with no id.");

            var breakdown = cap.SellerReceivableBreakdown;
            var capturedAmount = ParseMoney(cap.Amount?.Value) ?? amount;

            return new CaptureResult(
                CaptureId: cap.Id,
                Status: cap.Status?.Value ?? "",
                Amount: capturedAmount,
                PayPalFee: ParseMoney(breakdown?.PaypalFee?.Value),
                NetAmount: ParseMoney(breakdown?.NetAmount?.Value),
                Currency: cap.Amount?.CurrencyCode ?? currency);
        }
        catch (SdkException<CaptureAuthorizedPaymentError> ex)
        {
            throw Gateway("capture authorization",
                ex.Error.TryGetError(out var e) ? e : null,
                ex.Error.TryGetRawError(out var raw) ? raw : null);
        }
        catch (Exception ex) when (IsTransport(ex)) { throw Unreachable(ex); }
        catch (JsonException ex) { throw Unprocessable(ex); }
    }

    public async Task<RefundResult> RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey, CancellationToken ct = default)
    {
        try
        {
            // Omit the amount for a full refund of whatever remains captured (PayPal computes it).
            RefundRequest? body = amount.HasValue
                ? new RefundRequest { Amount = new Money { CurrencyCode = currency, Value = Money2(amount.Value) } }
                : null;

            var refund = await _client.Payments.RefundCapturedPayment(
                captureId: captureId,
                payPalMockResponse: null,
                payPalRequestId: idempotencyKey,
                payPalAuthAssertion: null,
                body: body,
                prefer: Representation,
                ct: ct);

            if (refund.Id is null)
                throw new PaymentGatewayException("PayPal returned a refund with no id.");

            return new RefundResult(
                RefundId: refund.Id,
                Status: refund.Status?.Value ?? "",
                Amount: ParseMoney(refund.Amount?.Value) ?? amount ?? 0m);
        }
        catch (SdkException<RefundCapturedPaymentError> ex)
        {
            throw Gateway("refund capture",
                ex.Error.TryGetError(out var e) ? e : null,
                ex.Error.TryGetRawError(out var raw) ? raw : null);
        }
        catch (Exception ex) when (IsTransport(ex)) { throw Unreachable(ex); }
        catch (JsonException ex) { throw Unprocessable(ex); }
    }

    public async Task<VaultedCardResult> VaultCardAsync(CardDetails card, CancellationToken ct = default)
    {
        try
        {
            var body = new PaymentTokenRequest
            {
                PaymentSource = new PaymentTokenRequestPaymentSource
                {
                    Card = new PaymentTokenRequestCard
                    {
                        Name = card.Name,
                        Number = card.Number,
                        Expiry = card.Expiry,
                        SecurityCode = card.SecurityCode,
                        BillingAddress = MapAddress(card.BillingAddress)
                    }
                }
            };

            var resp = await _client.Vault.CreatePaymentToken(
                payPalRequestId: Guid.NewGuid().ToString(),
                body: body,
                ct: ct);

            if (resp.Id is null)
                throw new PaymentGatewayException("PayPal returned a vaulted card with no id.");

            var entity = resp.PaymentSource?.Card;
            return new VaultedCardResult(
                VaultId: resp.Id,
                Brand: entity?.Brand?.Value ?? "",
                LastDigits: entity?.LastDigits ?? "",
                Expiry: entity?.Expiry ?? "");
        }
        catch (SdkException<CreatePaymentTokenError> ex)
        {
            throw GatewayVault("vault card",
                ex.Error.TryGetError1(out var e) ? e : null,
                ex.Error.TryGetRawError(out var raw) ? raw : null);
        }
        catch (Exception ex) when (IsTransport(ex)) { throw Unreachable(ex); }
        catch (JsonException ex) { throw Unprocessable(ex); }
    }

    public async Task DeleteVaultedCardAsync(string vaultId, CancellationToken ct = default)
    {
        try
        {
            await _client.Vault.DeletePaymentToken(id: vaultId, ct: ct);
        }
        catch (SdkException<DeletePaymentTokenError> ex)
        {
            throw GatewayVault("delete vaulted card",
                ex.Error.TryGetError1(out var e) ? e : null,
                ex.Error.TryGetRawError(out var raw) ? raw : null);
        }
        catch (Exception ex) when (IsTransport(ex)) { throw Unreachable(ex); }
        catch (JsonException ex) { throw Unprocessable(ex); }
    }

    public async Task<IReadOnlyList<ReconciliationTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var results = new List<ReconciliationTransaction>();
        string start = from.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        string end = to.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

        try
        {
            int page = 1;
            int totalPages;
            do
            {
                var resp = await _client.TransactionSearch.SearchTransactions(
                    startDate: start,
                    endDate: end,
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
                    ct: ct);

                totalPages = resp.TotalPages ?? 1;

                if (resp.TransactionDetails is not null)
                {
                    foreach (var detail in resp.TransactionDetails)
                    {
                        var info = detail.TransactionInfo;
                        if (info?.TransactionId is null) continue;

                        results.Add(new ReconciliationTransaction(
                            TransactionId: info.TransactionId,
                            Amount: ParseMoney(info.TransactionAmount?.Value) ?? 0m,
                            Currency: info.TransactionAmount?.CurrencyCode ?? "",
                            Status: info.TransactionStatus ?? "",
                            InitiatedAt: ParseDate(info.TransactionInitiationDate) ?? default));
                    }
                }

                page++;
            }
            while (page <= totalPages);

            return results;
        }
        catch (SdkException<RawError> ex)
        {
            throw new PaymentGatewayException(
                $"PayPal rejected the transaction-search request (HTTP {(int)ex.Error.StatusCode}).");
        }
        catch (Exception ex) when (IsTransport(ex)) { throw Unreachable(ex); }
        catch (JsonException ex) { throw Unprocessable(ex); }
    }

    // ---- helpers -------------------------------------------------------------------------------

    private static AuthorizationResult PayerAction() => new(true, null, null, null, null);

    private static Address? MapAddress(BillingAddress? a)
    {
        if (a is null) return null;
        return new Address
        {
            AddressLine1 = a.Line1,
            AddressLine2 = a.Line2,
            AdminArea2 = a.City,
            AdminArea1 = a.State,
            PostalCode = a.PostalCode,
            CountryCode = a.CountryCode
        };
    }

    private static string Money2(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static decimal? ParseMoney(string? value) =>
        value is not null && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            ? d : null;

    private static DateTimeOffset? ParseDate(string? value) =>
        value is not null && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)
            ? d : null;

    private static bool IsTransport(Exception ex) => ex is HttpRequestException or TaskCanceledException;

    private static PaymentGatewayException Unreachable(Exception ex) =>
        new("PayPal is currently unreachable. Please try again.", ex);

    private static PaymentGatewayException Unprocessable(JsonException ex) =>
        new("PayPal returned a response that could not be processed.", ex);

    private static PaymentGatewayException Gateway(string op, Error? e, RawError? raw)
    {
        if (e is not null)
            return new PaymentGatewayException($"PayPal rejected the {op} request: {e.Name} — {e.Message}", e.Name, e.DebugId);
        if (raw is not null)
            return new PaymentGatewayException($"PayPal rejected the {op} request (HTTP {(int)raw.StatusCode}).");
        return new PaymentGatewayException($"PayPal rejected the {op} request.");
    }

    private static PaymentGatewayException GatewayVault(string op, Error1? e, RawError? raw)
    {
        if (e is not null)
            return new PaymentGatewayException($"PayPal rejected the {op} request: {e.Name} — {e.Message}", e.Name, e.DebugId);
        if (raw is not null)
            return new PaymentGatewayException($"PayPal rejected the {op} request (HTTP {(int)raw.StatusCode}).");
        return new PaymentGatewayException($"PayPal rejected the {op} request.");
    }
}
