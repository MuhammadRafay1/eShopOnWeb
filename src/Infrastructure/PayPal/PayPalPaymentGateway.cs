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
using Microsoft.eShopWeb.ApplicationCore.Models.Payments;
using PayPalServerSdk;
using PayPalServerSdk.Core.ErrorResponse;
using PayPalServerSdk.Core.Exceptions;
using PayPalServerSdk.Errors;
using PayPalServerSdk.Models;
using PayPalServerSdk.Models.Enums;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// The sole consumer of the PayPal .NET SDK in this application. Implements <see cref="IPaymentGateway"/>
/// strictly from the grounded PayPal SDK contract sheet (paypal-plan.md) produced for this integration —
/// every operation, parameter and error accessor below is taken from that sheet, not from memory.
/// </summary>
public class PayPalPaymentGateway : IPaymentGateway
{
    private readonly PayPalServerSdkClient _client;

    public PayPalPaymentGateway(PayPalServerSdkClient client)
    {
        _client = client;
    }

    public async Task<AuthorizationResult> AuthorizeAsync(AuthorizeCardPaymentRequest request, CancellationToken ct = default)
    {
        if (request.Card is null && string.IsNullOrEmpty(request.VaultId))
        {
            throw new RequestValidationException("A card or a saved payment method id is required to authorize a payment.");
        }

        string payPalOrderId;
        AuthorizationWithAdditionalData? authorization = null;

        if (string.IsNullOrEmpty(request.ExistingPayPalOrderId))
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
                            CurrencyCode = request.CurrencyCode,
                            Value = FormatAmount(request.Amount)
                        },
                        InvoiceId = request.CorrelationReference,
                        CustomId = request.CustomId
                    }
                },
                PaymentSource = new PaymentSource
                {
                    Card = BuildCardRequest(request.Card, request.VaultId)
                }
            };

            Order orderResponse;
            try
            {
                orderResponse = await _client.Orders.CreateOrder(
                    payPalMockResponse: null,
                    payPalRequestId: $"{request.RequestKeyBase}:create",
                    payPalPartnerAttributionId: null,
                    payPalClientMetadataId: null,
                    payPalAuthAssertion: null,
                    body: orderRequest,
                    prefer: "return=representation",
                    ct: ct);
            }
            catch (SdkException<CreateOrderError> ex)
            {
                if (ex.Error.TryGetError(out var typedError))
                    throw RejectedFrom(typedError, "order creation");
                if (ex.Error.TryGetRawError(out var raw))
                    throw GatewayFailureFrom(raw, "order creation");
                throw new PaymentGatewayException("PayPal order creation failed with an unrecognized error.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new PaymentGatewayException("PayPal was unreachable while creating the order.", ex);
            }
            catch (JsonException ex)
            {
                throw new PaymentGatewayException("PayPal returned a response that could not be processed while creating the order.", ex);
            }

            GuardOrderNotPendingPayerAction(orderResponse.Status, orderResponse.Links);

            if (string.IsNullOrEmpty(orderResponse.Id))
            {
                throw new PaymentGatewayException("PayPal did not return an order id.");
            }

            payPalOrderId = orderResponse.Id;

            // Confirmed against live sandbox traffic: for a direct card (or a saved card via vault_id)
            // with intent=AUTHORIZE, PayPal executes the authorization SYNCHRONOUSLY inside CreateOrder
            // itself (the order comes back Status=Completed with purchase_units[].payments.authorizations
            // already populated). Calling AuthorizeOrder afterwards on such an order is invalid — PayPal
            // rejects it with ORDER_ALREADY_AUTHORIZED / TRANSACTION_REFUSED. Use what CreateOrder already
            // returned instead of unconditionally calling AuthorizeOrder next.
            authorization = orderResponse.PurchaseUnits?.FirstOrDefault()?.Payments?.Authorizations?.FirstOrDefault();
        }
        else
        {
            payPalOrderId = request.ExistingPayPalOrderId;
        }

        if (authorization is null)
        {
            // Either resuming a prior partial attempt (ExistingPayPalOrderId set, no local authorization
            // recorded yet), or CreateOrder left the order awaiting an explicit authorize call.
            OrderAuthorizeResponse authResponse;
            try
            {
                authResponse = await _client.Orders.AuthorizeOrder(
                    id: payPalOrderId,
                    payPalMockResponse: null,
                    payPalRequestId: $"{request.RequestKeyBase}:auth",
                    payPalClientMetadataId: null,
                    payPalAuthAssertion: null,
                    body: null,
                    prefer: "return=representation",
                    ct: ct);
            }
            catch (SdkException<AuthorizeOrderError> ex)
            {
                if (ex.Error.TryGetError(out var typedError))
                    throw RejectedFrom(typedError, "authorization");
                if (ex.Error.TryGetRawError(out var raw))
                    throw GatewayFailureFrom(raw, "authorization");
                throw new PaymentGatewayException("PayPal authorization failed with an unrecognized error.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new PaymentGatewayException("PayPal was unreachable while authorizing the payment.", ex);
            }
            catch (JsonException ex)
            {
                throw new PaymentGatewayException("PayPal returned a response that could not be processed while authorizing the payment.", ex);
            }

            GuardOrderNotPendingPayerAction(authResponse.Status, authResponse.Links);
            authorization = authResponse.PurchaseUnits?.FirstOrDefault()?.Payments?.Authorizations?.FirstOrDefault();
        }

        if (authorization is null || string.IsNullOrEmpty(authorization.Id))
        {
            throw new PaymentGatewayException("PayPal did not return an authorization for this order.");
        }

        return new AuthorizationResult
        {
            PayPalOrderId = payPalOrderId,
            AuthorizationId = authorization.Id,
            Status = authorization.Status?.Value ?? string.Empty,
            HeldAmount = ParseAmount(authorization.Amount?.Value),
            ExpiresAt = ParseDate(authorization.ExpirationTime)
        };
    }

    public async Task<ReauthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount, string currencyCode, string requestKey, CancellationToken ct = default)
    {
        var body = new ReauthorizeRequest
        {
            Amount = new Money { CurrencyCode = currencyCode, Value = FormatAmount(amount) }
        };

        PaymentAuthorization response;
        try
        {
            response = await _client.Payments.ReauthorizePayment(
                authorizationId: authorizationId,
                payPalRequestId: requestKey,
                payPalAuthAssertion: null,
                body: body,
                prefer: "return=representation",
                ct: ct);
        }
        catch (SdkException<ReauthorizePaymentError> ex)
        {
            if (ex.Error.TryGetError(out var typedError))
                throw RejectedFrom(typedError, "re-authorization");
            if (ex.Error.TryGetNoContent(out var noContent))
                throw GatewayFailureFrom(noContent, "re-authorization");
            if (ex.Error.TryGetRawError(out var raw))
                throw GatewayFailureFrom(raw, "re-authorization");
            throw new PaymentGatewayException("PayPal re-authorization failed with an unrecognized error.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new PaymentGatewayException("PayPal was unreachable while re-authorizing the payment.", ex);
        }
        catch (JsonException ex)
        {
            throw new PaymentGatewayException("PayPal returned a response that could not be processed while re-authorizing the payment.", ex);
        }

        // PaymentAuthorization carries no Id field per the contract sheet — the resource stays identified
        // by the authorizationId we called with.
        return new ReauthorizationResult
        {
            AuthorizationId = authorizationId,
            Status = response.Status?.Value ?? string.Empty,
            ExpiresAt = ParseDate(response.ExpirationTime)
        };
    }

    public async Task<CaptureResult> CaptureAsync(string authorizationId, string requestKey, CancellationToken ct = default)
    {
        CapturedPayment response;
        try
        {
            response = await _client.Payments.CaptureAuthorizedPayment(
                authorizationId: authorizationId,
                payPalMockResponse: null,
                payPalRequestId: requestKey,
                payPalAuthAssertion: null,
                body: null,
                prefer: "return=representation",
                ct: ct);
        }
        catch (SdkException<CaptureAuthorizedPaymentError> ex)
        {
            if (ex.Error.TryGetError(out var typedError))
                throw RejectedFrom(typedError, "capture");
            if (ex.Error.TryGetNoContent(out var noContent))
                throw GatewayFailureFrom(noContent, "capture");
            if (ex.Error.TryGetRawError(out var raw))
                throw GatewayFailureFrom(raw, "capture");
            throw new PaymentGatewayException("PayPal capture failed with an unrecognized error.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new PaymentGatewayException("PayPal was unreachable while capturing the payment.", ex);
        }
        catch (JsonException ex)
        {
            throw new PaymentGatewayException("PayPal returned a response that could not be processed while capturing the payment.", ex);
        }

        if (string.IsNullOrEmpty(response.Id))
        {
            throw new PaymentGatewayException("PayPal did not return a capture id.");
        }

        var breakdown = response.SellerReceivableBreakdown;
        return new CaptureResult
        {
            CaptureId = response.Id,
            Status = response.Status?.Value ?? string.Empty,
            CapturedAmount = ParseAmount(response.Amount?.Value),
            PayPalFee = ParseAmountOrNull(breakdown?.PaypalFee?.Value),
            NetAmount = ParseAmountOrNull(breakdown?.NetAmount?.Value)
        };
    }

    public async Task VoidAsync(string authorizationId, string requestKey, CancellationToken ct = default)
    {
        try
        {
            // return=minimal would leave the response body empty; the SDK always deserializes
            // VoidPayment's declared PaymentAuthorization return type regardless, so an empty body
            // throws JsonException. Use return=representation like every other write call we read.
            await _client.Payments.VoidPayment(
                authorizationId: authorizationId,
                payPalMockResponse: null,
                payPalAuthAssertion: null,
                payPalRequestId: requestKey,
                prefer: "return=representation",
                ct: ct);
        }
        catch (SdkException<VoidPaymentError> ex)
        {
            if (ex.Error.TryGetError(out var typedError))
                throw RejectedFrom(typedError, "void");
            if (ex.Error.TryGetNoContent(out var noContent))
                throw GatewayFailureFrom(noContent, "void");
            if (ex.Error.TryGetRawError(out var raw))
                throw GatewayFailureFrom(raw, "void");
            throw new PaymentGatewayException("PayPal void failed with an unrecognized error.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new PaymentGatewayException("PayPal was unreachable while voiding the authorization.", ex);
        }
        catch (JsonException ex)
        {
            throw new PaymentGatewayException("PayPal returned a response that could not be processed while voiding the authorization.", ex);
        }
    }

    public async Task<RefundResult> RefundAsync(string captureId, decimal? amount, string currencyCode, string idempotencyKey, CancellationToken ct = default)
    {
        RefundRequest? body = amount.HasValue
            ? new RefundRequest { Amount = new Money { CurrencyCode = currencyCode, Value = FormatAmount(amount.Value) } }
            : null;

        PayPalServerSdk.Models.Refund response;
        try
        {
            response = await _client.Payments.RefundCapturedPayment(
                captureId: captureId,
                payPalMockResponse: null,
                payPalRequestId: idempotencyKey,
                payPalAuthAssertion: null,
                body: body,
                prefer: "return=representation",
                ct: ct);
        }
        catch (SdkException<RefundCapturedPaymentError> ex)
        {
            if (ex.Error.TryGetError(out var typedError))
                throw RejectedFrom(typedError, "refund");
            if (ex.Error.TryGetNoContent(out var noContent))
                throw GatewayFailureFrom(noContent, "refund");
            if (ex.Error.TryGetRawError(out var raw))
                throw GatewayFailureFrom(raw, "refund");
            throw new PaymentGatewayException("PayPal refund failed with an unrecognized error.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new PaymentGatewayException("PayPal was unreachable while issuing the refund.", ex);
        }
        catch (JsonException ex)
        {
            throw new PaymentGatewayException("PayPal returned a response that could not be processed while issuing the refund.", ex);
        }

        if (string.IsNullOrEmpty(response.Id))
        {
            throw new PaymentGatewayException("PayPal did not return a refund id.");
        }

        return new RefundResult
        {
            RefundId = response.Id,
            Status = response.Status?.Value ?? string.Empty,
            Amount = ParseAmount(response.Amount?.Value)
        };
    }

    public async Task<VaultedCardResult> VaultCardAsync(CardDetails card, CancellationToken ct = default)
    {
        var body = new PaymentTokenRequest
        {
            PaymentSource = new PaymentTokenRequestPaymentSource
            {
                Card = new PaymentTokenRequestCard
                {
                    Name = card.CardholderName,
                    Number = card.Number,
                    Expiry = card.Expiry,
                    SecurityCode = card.SecurityCode,
                    BillingAddress = ToAddress(card.BillingAddress)
                }
            }
        };

        PaymentTokenResponse response;
        try
        {
            response = await _client.Vault.CreatePaymentToken(
                payPalRequestId: Guid.NewGuid().ToString(),
                body: body,
                ct: ct);
        }
        catch (SdkException<CreatePaymentTokenError> ex)
        {
            if (ex.Error.TryGetError1(out var typedError))
                throw RejectedFrom(typedError, "card vaulting");
            if (ex.Error.TryGetRawError(out var raw))
                throw GatewayFailureFrom(raw, "card vaulting");
            throw new PaymentGatewayException("PayPal card vaulting failed with an unrecognized error.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new PaymentGatewayException("PayPal was unreachable while saving the card.", ex);
        }
        catch (JsonException ex)
        {
            throw new PaymentGatewayException("PayPal returned a response that could not be processed while saving the card.", ex);
        }

        GuardNoPayerActionLink(response.Links);

        var cardEntity = response.PaymentSource?.Card;
        if (string.IsNullOrEmpty(response.Id) || cardEntity is null)
        {
            throw new PaymentGatewayException("PayPal did not return a usable saved-card token.");
        }

        return new VaultedCardResult
        {
            VaultId = response.Id,
            Brand = cardEntity.Brand?.Value ?? "UNKNOWN",
            LastDigits = cardEntity.LastDigits ?? string.Empty,
            Expiry = cardEntity.Expiry ?? card.Expiry,
            CardholderName = cardEntity.Name,
            PayPalCustomerId = response.Customer?.Id
        };
    }

    public async Task DeleteVaultedCardAsync(string vaultId, CancellationToken ct = default)
    {
        try
        {
            await _client.Vault.DeletePaymentToken(id: vaultId, ct: ct);
        }
        catch (SdkException<DeletePaymentTokenError> ex)
        {
            if (ex.Error.TryGetError1(out var typedError))
                throw RejectedFrom(typedError, "saved-card deletion");
            if (ex.Error.TryGetRawError(out var raw))
                throw GatewayFailureFrom(raw, "saved-card deletion");
            throw new PaymentGatewayException("PayPal saved-card deletion failed with an unrecognized error.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new PaymentGatewayException("PayPal was unreachable while deleting the saved card.", ex);
        }
    }

    public async Task<IReadOnlyList<GatewayTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
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
                    ct: ct);
            }
            catch (SdkException<RawError> ex)
            {
                throw GatewayFailureFrom(ex.Error, "transaction search");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new PaymentGatewayException("PayPal was unreachable while searching transactions.", ex);
            }
            catch (JsonException ex)
            {
                throw new PaymentGatewayException("PayPal returned a response that could not be processed while searching transactions.", ex);
            }

            if (response.TransactionDetails is not null)
            {
                foreach (var detail in response.TransactionDetails)
                {
                    var info = detail.TransactionInfo;
                    if (info is null) continue;

                    results.Add(new GatewayTransaction
                    {
                        TransactionId = info.TransactionId ?? string.Empty,
                        Status = info.TransactionStatus,
                        Amount = ParseAmountOrNull(info.TransactionAmount?.Value),
                        CurrencyCode = info.TransactionAmount?.CurrencyCode,
                        InvoiceId = info.InvoiceId,
                        CustomField = info.CustomField
                    });
                }
            }

            totalPages = response.TotalPages ?? 1;
            page++;
        } while (page <= totalPages);

        return results;
    }

    private static CardRequest BuildCardRequest(CardDetails? card, string? vaultId)
    {
        if (!string.IsNullOrEmpty(vaultId))
        {
            return new CardRequest { VaultId = vaultId };
        }

        if (card is null)
        {
            throw new RequestValidationException("A card or a saved payment method id is required.");
        }

        return new CardRequest
        {
            Name = card.CardholderName,
            Number = card.Number,
            Expiry = card.Expiry,
            SecurityCode = card.SecurityCode,
            BillingAddress = ToAddress(card.BillingAddress)
        };
    }

    private static Address? ToAddress(BillingAddressInput? input)
    {
        if (input is null) return null;

        return new Address
        {
            AddressLine1 = input.AddressLine1,
            AddressLine2 = input.AddressLine2,
            AdminArea2 = input.AdminArea2,
            AdminArea1 = input.AdminArea1,
            PostalCode = input.PostalCode,
            CountryCode = input.CountryCode
        };
    }

    private static string FormatAmount(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

    private static decimal ParseAmount(string? value) =>
        string.IsNullOrEmpty(value) ? 0m : decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    private static decimal? ParseAmountOrNull(string? value) =>
        string.IsNullOrEmpty(value) ? null : decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    private static DateTimeOffset? ParseDate(string? value) =>
        !string.IsNullOrEmpty(value) &&
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// Defensive check per the task's stop-condition: if PayPal ever asks for shopper approval (3DS /
    /// PAYER_ACTION_REQUIRED) on a direct card, we do not build a browser round-trip — we stop and
    /// surface it as an operator-actionable rejection instead.
    /// </summary>
    private static void GuardOrderNotPendingPayerAction(OrderStatus? status, IReadOnlyList<LinkDescription>? links)
    {
        if (status == OrderStatus.PayerActionRequired || HasPayerActionLink(links))
        {
            throw new PaymentRejectedException(
                "PayPal requires additional shopper verification (a payer-action/3DS challenge) that this integration does not support. The payment was not authorized.");
        }
    }

    private static void GuardNoPayerActionLink(IReadOnlyList<LinkDescription>? links)
    {
        if (HasPayerActionLink(links))
        {
            throw new PaymentRejectedException(
                "PayPal requires additional shopper verification (a payer-action challenge) that this integration does not support.");
        }
    }

    private static bool HasPayerActionLink(IReadOnlyList<LinkDescription>? links) =>
        links?.Any(l => l.Rel is not null && l.Rel.Contains("payer-action", StringComparison.OrdinalIgnoreCase)) ?? false;

    private static PaymentRejectedException RejectedFrom(Error error, string operation)
    {
        var reason = error.Details?.FirstOrDefault()?.Issue ?? error.Message;
        return new PaymentRejectedException($"PayPal rejected the {operation}: {reason}");
    }

    private static PaymentRejectedException RejectedFrom(Error1 error, string operation)
    {
        var reason = error.Details?.FirstOrDefault()?.Issue ?? error.Message;
        return new PaymentRejectedException($"PayPal rejected the {operation}: {reason}");
    }

    private static PaymentGatewayException GatewayFailureFrom(RawError raw, string operation)
    {
        string body;
        try
        {
            body = raw.ReadAsString();
        }
        catch
        {
            body = "(unreadable response body)";
        }

        return new PaymentGatewayException($"PayPal {operation} failed with HTTP {(int)raw.StatusCode}: {body}");
    }
}
