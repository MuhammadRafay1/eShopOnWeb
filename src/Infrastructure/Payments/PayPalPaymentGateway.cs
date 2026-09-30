using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using PayPalServerSdk;
using PayPalServerSdk.Core.ErrorResponse;
using PayPalServerSdk.Core.Exceptions;
using PayPalServerSdk.Errors;
using PayPalServerSdk.Models;
using PayPalServerSdk.Models.Enums;
using PayPalAddress = PayPalServerSdk.Models.Address;

namespace Microsoft.eShopWeb.Infrastructure.Payments;

// The only class in this solution that references the PayPal SDK. Maps SDK models to
// ApplicationCore-owned DTOs and translates every SDK failure into ApplicationCore exceptions.
public class PayPalPaymentGateway : IPaymentGateway
{
    private readonly PayPalServerSdkClient _client;

    public PayPalPaymentGateway(PayPalServerSdkClient client)
    {
        _client = client;
    }

    public async Task<GatewayCreatedOrder> CreateAuthorizeOrderAsync(decimal amount, string currency, string invoiceReference, string idempotencyKey, CancellationToken ct)
    {
        var body = new OrderRequest
        {
            Intent = CheckoutPaymentIntent.Authorize,
            PurchaseUnits = new List<PurchaseUnitRequest>
            {
                new PurchaseUnitRequest
                {
                    Amount = new AmountWithBreakdown
                    {
                        CurrencyCode = currency,
                        Value = AmountFormatter.ToPayPalValue(amount, currency)
                    },
                    InvoiceId = invoiceReference
                }
            }
        };

        Order response;
        try
        {
            response = await _client.Orders.CreateOrder(
                payPalMockResponse: null,
                payPalRequestId: idempotencyKey,
                payPalPartnerAttributionId: null,
                payPalClientMetadataId: null,
                payPalAuthAssertion: null,
                body: body,
                prefer: "return=representation",
                requestOptions: null,
                ct: ct);
        }
        catch (SdkException<CreateOrderError> ex)
        {
            if (ex.Error.TryGetError(out var err)) throw TranslateTypedError(err, "CreateOrder");
            if (ex.Error.TryGetRawError(out var raw)) throw TranslateRawError(raw, "CreateOrder");
            throw UnrecognisedError("CreateOrder");
        }
        catch (JsonException ex)
        {
            throw UnprocessableResponse("CreateOrder", ex);
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable("CreateOrder", ex);
        }

        return new GatewayCreatedOrder(response.Id ?? string.Empty, response.Status?.Value ?? string.Empty);
    }

    public async Task<GatewayAuthorization> AuthorizeWithCardAsync(string payPalOrderId, CardInput card, string idempotencyKey, CancellationToken ct)
    {
        var body = new OrderAuthorizeRequest
        {
            PaymentSource = new OrderAuthorizeRequestPaymentSource
            {
                Card = BuildCardRequest(card)
            }
        };

        return await AuthorizeAsync(payPalOrderId, body, idempotencyKey, ct);
    }

    public async Task<GatewayAuthorization> AuthorizeWithVaultAsync(string payPalOrderId, string vaultId, string idempotencyKey, CancellationToken ct)
    {
        var body = new OrderAuthorizeRequest
        {
            PaymentSource = new OrderAuthorizeRequestPaymentSource
            {
                Card = new CardRequest { VaultId = vaultId }
            }
        };

        return await AuthorizeAsync(payPalOrderId, body, idempotencyKey, ct);
    }

    private async Task<GatewayAuthorization> AuthorizeAsync(string payPalOrderId, OrderAuthorizeRequest body, string idempotencyKey, CancellationToken ct)
    {
        OrderAuthorizeResponse response;
        try
        {
            response = await _client.Orders.AuthorizeOrder(
                id: payPalOrderId,
                payPalMockResponse: null,
                payPalRequestId: idempotencyKey,
                payPalClientMetadataId: null,
                payPalAuthAssertion: null,
                body: body,
                prefer: "return=representation",
                requestOptions: null,
                ct: ct);
        }
        catch (SdkException<AuthorizeOrderError> ex)
        {
            if (ex.Error.TryGetError(out var err)) throw TranslateTypedError(err, "AuthorizeOrder");
            if (ex.Error.TryGetRawError(out var raw)) throw TranslateRawError(raw, "AuthorizeOrder");
            throw UnrecognisedError("AuthorizeOrder");
        }
        catch (JsonException ex)
        {
            throw UnprocessableResponse("AuthorizeOrder", ex);
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable("AuthorizeOrder", ex);
        }

        const string challengeMessage = "Card requires buyer approval in a browser (3DS/PAYER_ACTION_REQUIRED); server-side card payment cannot complete.";

        if (response.Status == OrderStatus.PayerActionRequired)
        {
            throw new PaymentChallengeRequiredException(challengeMessage);
        }

        var authorization = response.PurchaseUnits?.FirstOrDefault()?.Payments?.Authorizations?.FirstOrDefault();
        if (authorization?.Id is null)
        {
            var hasChallengeLink = response.Links?.Any(l =>
                l.Rel is not null &&
                (l.Rel.Contains("payer-action", StringComparison.OrdinalIgnoreCase) ||
                 l.Rel.Contains("approve", StringComparison.OrdinalIgnoreCase) ||
                 l.Rel.Contains("3ds", StringComparison.OrdinalIgnoreCase))) ?? false;

            var isApproved = response.Status == OrderStatus.Approved || response.Status == OrderStatus.Completed;

            if (hasChallengeLink || !isApproved)
            {
                throw new PaymentChallengeRequiredException(challengeMessage);
            }

            throw new PaymentGatewayException("PayPal did not return an authorization for this order.");
        }

        return new GatewayAuthorization(authorization.Id, authorization.Status?.Value ?? string.Empty, ParseTimestamp(authorization.ExpirationTime));
    }

    public async Task<GatewayCapture> CaptureAsync(string authorizationId, string idempotencyKey, CancellationToken ct)
    {
        CapturedPayment response;
        try
        {
            response = await _client.Payments.CaptureAuthorizedPayment(
                authorizationId: authorizationId,
                payPalMockResponse: null,
                payPalRequestId: idempotencyKey,
                payPalAuthAssertion: null,
                body: null,
                prefer: "return=representation",
                requestOptions: null,
                ct: ct);
        }
        catch (SdkException<CaptureAuthorizedPaymentError> ex)
        {
            if (ex.Error.TryGetError(out var err)) throw TranslateTypedError(err, "CaptureAuthorizedPayment");
            if (ex.Error.TryGetNoContent(out var noContent)) throw TranslateRawError(noContent, "CaptureAuthorizedPayment");
            if (ex.Error.TryGetRawError(out var raw)) throw TranslateRawError(raw, "CaptureAuthorizedPayment");
            throw UnrecognisedError("CaptureAuthorizedPayment");
        }
        catch (JsonException ex)
        {
            throw UnprocessableResponse("CaptureAuthorizedPayment", ex);
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable("CaptureAuthorizedPayment", ex);
        }

        var breakdown = response.SellerReceivableBreakdown;
        var gross = breakdown is not null
            ? AmountFormatter.FromPayPalValue(breakdown.GrossAmount.Value)
            : AmountFormatter.FromPayPalValue(response.Amount?.Value ?? "0");
        var fee = breakdown?.PaypalFee is not null ? AmountFormatter.FromPayPalValue(breakdown.PaypalFee.Value) : (decimal?)null;
        var net = breakdown?.NetAmount is not null ? AmountFormatter.FromPayPalValue(breakdown.NetAmount.Value) : (decimal?)null;
        var currency = breakdown?.GrossAmount.CurrencyCode ?? response.Amount?.CurrencyCode ?? string.Empty;

        return new GatewayCapture(response.Id ?? string.Empty, response.Status?.Value ?? string.Empty, gross, fee, net, currency);
    }

    public async Task<GatewayAuthorization> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string idempotencyKey, CancellationToken ct)
    {
        var body = new ReauthorizeRequest
        {
            Amount = new Money { CurrencyCode = currency, Value = AmountFormatter.ToPayPalValue(amount, currency) }
        };

        PaymentAuthorization response;
        try
        {
            response = await _client.Payments.ReauthorizePayment(
                authorizationId: authorizationId,
                payPalRequestId: idempotencyKey,
                payPalAuthAssertion: null,
                body: body,
                prefer: "return=representation",
                requestOptions: null,
                ct: ct);
        }
        catch (SdkException<ReauthorizePaymentError> ex)
        {
            if (ex.Error.TryGetError(out var err)) throw TranslateTypedError(err, "ReauthorizePayment");
            if (ex.Error.TryGetNoContent(out var noContent)) throw TranslateRawError(noContent, "ReauthorizePayment");
            if (ex.Error.TryGetRawError(out var raw)) throw TranslateRawError(raw, "ReauthorizePayment");
            throw UnrecognisedError("ReauthorizePayment");
        }
        catch (JsonException ex)
        {
            throw UnprocessableResponse("ReauthorizePayment", ex);
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable("ReauthorizePayment", ex);
        }

        return new GatewayAuthorization(response.Id ?? string.Empty, response.Status?.Value ?? string.Empty, ParseTimestamp(response.ExpirationTime));
    }

    public async Task VoidAsync(string authorizationId, CancellationToken ct)
    {
        try
        {
            await _client.Payments.VoidPayment(
                authorizationId: authorizationId,
                payPalMockResponse: null,
                payPalAuthAssertion: null,
                payPalRequestId: $"void-{authorizationId}",
                prefer: "return=minimal",
                requestOptions: null,
                ct: ct);
        }
        catch (SdkException<VoidPaymentError> ex)
        {
            if (ex.Error.TryGetError(out var err)) throw TranslateTypedError(err, "VoidPayment");
            if (ex.Error.TryGetNoContent(out var noContent)) throw TranslateRawError(noContent, "VoidPayment");
            if (ex.Error.TryGetRawError(out var raw)) throw TranslateRawError(raw, "VoidPayment");
            throw UnrecognisedError("VoidPayment");
        }
        catch (JsonException)
        {
            // Confirmed SDK gap (v1.0.1, source-checked): VoidPayment's generated success path is
            // wired unconditionally to JsonResponse.Create<PaymentAuthorization>() (Api/Payments.cs),
            // which always calls JsonSerializer.DeserializeAsync on the response stream with no
            // branch for an empty body — unlike DeletePaymentToken/PatchOrder, which correctly use
            // VoidResponse.Instance (no deserialization) for a genuinely bodyless success. PayPal's
            // void endpoint can legitimately answer 204 with no body; that 204 is inside
            // HttpStatusPolicy's default 200-299 success range (Core/HttpStatusPolicy.cs, shared
            // globally, no per-op override), so this JsonException can only come from the success
            // path for this operation — never from a malformed error body. It still carries no
            // status code, so it cannot itself prove the void completed: resolve via a confirmatory
            // read rather than treating the exception type as either success or failure.
            await ConfirmVoidedAsync(authorizationId, ct);
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable("VoidPayment", ex);
        }
    }

    private async Task ConfirmVoidedAsync(string authorizationId, CancellationToken ct)
    {
        PaymentAuthorization confirmation;
        try
        {
            confirmation = await _client.Payments.GetAuthorizedPayment(
                authorizationId: authorizationId,
                payPalMockResponse: null,
                payPalAuthAssertion: null,
                requestOptions: null,
                ct: ct);
        }
        catch (SdkException<GetAuthorizedPaymentError> ex)
        {
            if (ex.Error.TryGetError(out var err)) throw TranslateTypedError(err, "VoidPayment (confirmation read)");
            if (ex.Error.TryGetNoContent(out var noContent)) throw TranslateRawError(noContent, "VoidPayment (confirmation read)");
            if (ex.Error.TryGetRawError(out var raw)) throw TranslateRawError(raw, "VoidPayment (confirmation read)");
            throw UnrecognisedError("VoidPayment (confirmation read)");
        }
        catch (JsonException ex)
        {
            throw UnprocessableResponse("VoidPayment (confirmation read)", ex);
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable("VoidPayment (confirmation read)", ex);
        }

        if (confirmation.Status != AuthorizationStatus.Voided)
        {
            throw new PaymentGatewayException(
                "PayPal VoidPayment returned an empty response body (a known SDK v1.0.1 response-mapping " +
                $"gap) and the authorization's confirmed status is '{confirmation.Status?.Value ?? "unknown"}', " +
                "not Voided — the void did not complete.");
        }
    }

    public async Task<GatewayRefund> RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey, CancellationToken ct)
    {
        RefundRequest? body = amount.HasValue
            ? new RefundRequest { Amount = new Money { CurrencyCode = currency, Value = AmountFormatter.ToPayPalValue(amount.Value, currency) } }
            : null;

        Refund response;
        try
        {
            response = await _client.Payments.RefundCapturedPayment(
                captureId: captureId,
                payPalMockResponse: null,
                payPalRequestId: idempotencyKey,
                payPalAuthAssertion: null,
                body: body,
                prefer: "return=representation",
                requestOptions: null,
                ct: ct);
        }
        catch (SdkException<RefundCapturedPaymentError> ex)
        {
            if (ex.Error.TryGetError(out var err)) throw TranslateTypedError(err, "RefundCapturedPayment");
            if (ex.Error.TryGetNoContent(out var noContent)) throw TranslateRawError(noContent, "RefundCapturedPayment");
            if (ex.Error.TryGetRawError(out var raw)) throw TranslateRawError(raw, "RefundCapturedPayment");
            throw UnrecognisedError("RefundCapturedPayment");
        }
        catch (JsonException ex)
        {
            throw UnprocessableResponse("RefundCapturedPayment", ex);
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable("RefundCapturedPayment", ex);
        }

        var refundedAmount = response.Amount is not null ? AmountFormatter.FromPayPalValue(response.Amount.Value) : (amount ?? 0m);
        return new GatewayRefund(response.Id ?? string.Empty, response.Status?.Value ?? string.Empty, refundedAmount);
    }

    public async Task<GatewaySavedCard> CreateVaultCardAsync(CardInput card, string merchantCustomerId, string idempotencyKey, CancellationToken ct)
    {
        var body = new PaymentTokenRequest
        {
            Customer = new Customer { MerchantCustomerId = merchantCustomerId },
            PaymentSource = new PaymentTokenRequestPaymentSource
            {
                Card = new PaymentTokenRequestCard
                {
                    Number = card.Number,
                    Expiry = card.Expiry,
                    SecurityCode = card.SecurityCode,
                    Name = card.CardholderName,
                    BillingAddress = BuildPayPalAddress(card.BillingAddress)
                }
            }
        };

        PaymentTokenResponse response;
        try
        {
            response = await _client.Vault.CreatePaymentToken(
                payPalRequestId: idempotencyKey,
                body: body,
                requestOptions: null,
                ct: ct);
        }
        catch (SdkException<CreatePaymentTokenError> ex)
        {
            if (ex.Error.TryGetError1(out var err)) throw TranslateTypedError1(err, "CreatePaymentToken");
            if (ex.Error.TryGetRawError(out var raw)) throw TranslateRawError(raw, "CreatePaymentToken");
            throw UnrecognisedError("CreatePaymentToken");
        }
        catch (JsonException ex)
        {
            throw UnprocessableResponse("CreatePaymentToken", ex);
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable("CreatePaymentToken", ex);
        }

        var cardEntity = response.PaymentSource?.Card;
        if (cardEntity is null || response.Id is null)
        {
            throw new PaymentGatewayException("PayPal did not return a saved card token.");
        }

        return new GatewaySavedCard(response.Id, cardEntity.Brand?.Value ?? "UNKNOWN", cardEntity.LastDigits ?? string.Empty, cardEntity.Expiry ?? string.Empty, cardEntity.Name);
    }

    public async Task DeleteVaultCardAsync(string vaultId, CancellationToken ct)
    {
        try
        {
            await _client.Vault.DeletePaymentToken(id: vaultId, requestOptions: null, ct: ct);
        }
        catch (SdkException<DeletePaymentTokenError> ex)
        {
            if (ex.Error.TryGetError1(out var err)) throw TranslateTypedError1(err, "DeletePaymentToken");
            if (ex.Error.TryGetRawError(out var raw))
            {
                if (raw.StatusCode == HttpStatusCode.NotFound)
                {
                    return; // already deleted — deleting a deleted card is a satisfied no-op, not a failure
                }

                throw TranslateRawError(raw, "DeletePaymentToken");
            }

            throw UnrecognisedError("DeletePaymentToken");
        }
        catch (JsonException ex)
        {
            throw UnprocessableResponse("DeletePaymentToken", ex);
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable("DeletePaymentToken", ex);
        }
    }

    // PayPal rejects a SearchTransactions window wider than 31 days (INVALID_REQUEST: "Date range is
    // greater than 31 days"), observed live in sandbox — this isn't an SDK contract limit, it's a
    // PayPal business rule the SDK does not enforce or chunk for you. Split the caller's requested
    // range into <=31-day windows so the report still covers the whole range end-to-end.
    public async Task<IReadOnlyList<GatewayTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var results = new List<GatewayTransaction>();
        if (to <= from)
        {
            return results;
        }

        var maxWindow = TimeSpan.FromDays(31);
        var windowStart = from;
        while (windowStart < to)
        {
            var windowEnd = windowStart + maxWindow;
            if (windowEnd > to)
            {
                windowEnd = to;
            }

            results.AddRange(await SearchTransactionsWindowAsync(windowStart, windowEnd, ct));
            windowStart = windowEnd;
        }

        return results;
    }

    private async Task<IReadOnlyList<GatewayTransaction>> SearchTransactionsWindowAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var results = new List<GatewayTransaction>();
        var startDate = from.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        var endDate = to.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

        var page = 1;
        var totalPages = 1;

        do
        {
            SearchResponse response;
            try
            {
                response = await _client.TransactionSearch.SearchTransactions(
                    startDate: startDate,
                    endDate: endDate,
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
            }
            catch (SdkException<RawError> ex)
            {
                throw TranslateRawError(ex.Error, "SearchTransactions");
            }
            catch (JsonException ex)
            {
                throw UnprocessableResponse("SearchTransactions", ex);
            }
            catch (HttpRequestException ex)
            {
                throw Unreachable("SearchTransactions", ex);
            }

            totalPages = response.TotalPages ?? 1;

            if (response.TransactionDetails is not null)
            {
                foreach (var detail in response.TransactionDetails)
                {
                    var info = detail.TransactionInfo;
                    if (info is null)
                    {
                        continue;
                    }

                    var amount = info.TransactionAmount is not null ? AmountFormatter.FromPayPalValue(info.TransactionAmount.Value) : 0m;
                    var currency = info.TransactionAmount?.CurrencyCode ?? string.Empty;

                    results.Add(new GatewayTransaction(info.TransactionId ?? string.Empty, info.TransactionStatus, amount, currency, info.InvoiceId, null));
                }
            }

            page++;
        } while (page <= totalPages);

        return results;
    }

    private static CardRequest BuildCardRequest(CardInput card) => new()
    {
        Number = card.Number,
        Expiry = card.Expiry,
        SecurityCode = card.SecurityCode,
        Name = card.CardholderName,
        BillingAddress = BuildPayPalAddress(card.BillingAddress)
    };

    private static PayPalAddress BuildPayPalAddress(CardBillingAddress address) => new()
    {
        AddressLine1 = address.Line1,
        AddressLine2 = address.Line2,
        AdminArea1 = address.State,
        AdminArea2 = address.City,
        PostalCode = address.PostalCode,
        CountryCode = address.CountryCode
    };

    private static DateTimeOffset? ParseTimestamp(string? timestamp)
    {
        if (!string.IsNullOrEmpty(timestamp) &&
            DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static PaymentGatewayException TranslateTypedError(Error err, string operation)
    {
        var detail = (err.Details?.Count ?? 0) > 0 ? (err.Details![0].Description ?? err.Details[0].Issue) : null;
        return new PaymentGatewayException($"PayPal {operation} was rejected: {detail ?? err.Message}", null, err.DebugId);
    }

    private static PaymentGatewayException TranslateTypedError1(Error1 err, string operation)
    {
        var detail = (err.Details?.Count ?? 0) > 0 ? (err.Details![0].Description ?? err.Details[0].Issue) : null;
        return new PaymentGatewayException($"PayPal {operation} was rejected: {detail ?? err.Message}", null, err.DebugId);
    }

    private static PaymentGatewayException TranslateRawError(RawError raw, string operation)
        => new($"PayPal {operation} failed with HTTP {(int)raw.StatusCode}: {SafeReadRawError(raw)}", (int)raw.StatusCode);

    private static string SafeReadRawError(RawError raw)
    {
        try
        {
            return raw.ReadAsString();
        }
        catch (JsonException)
        {
            return "(unreadable error body)";
        }
    }

    private static PaymentGatewayException UnrecognisedError(string operation)
        => new($"PayPal {operation} failed with an unrecognised error shape.");

    private static PaymentGatewayException UnprocessableResponse(string operation, Exception inner)
        => new($"PayPal {operation} returned a response that could not be processed.", null, null, inner);

    private static PaymentGatewayException Unreachable(string operation, Exception inner)
        => new($"PayPal {operation} could not be reached.", null, null, inner);
}
