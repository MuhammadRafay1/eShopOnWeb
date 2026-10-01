using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
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
using PayPalServerSdk.Requests.Orders;
using PayPalServerSdk.Requests.Payments;
using PayPalServerSdk.Requests.TransactionSearch;
using PayPalServerSdk.Requests.Vault;

namespace Microsoft.eShopWeb.Infrastructure.Services;

/// <summary>
/// The sole caller of PayPalServerSdk in this application. Every SDK exception is translated into a
/// <see cref="PaymentGatewayException"/> here (the error boundary); no PayPalServerSdk type leaves this
/// class. See pay-pal-server-sdk-plan.md (repo root PLAN.md) for the contract sheet this was built from.
/// </summary>
public class PayPalPaymentGateway : IPayPalPaymentGateway
{
    private static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(30);
    private const int MaxTransactionSearchPages = 50;

    private readonly PayPalServerSdkClient _client;

    public PayPalPaymentGateway(PayPalServerSdkClient client)
    {
        _client = client;
    }

    public async Task<PayPalOrderAuthorization> CreateOrderAndAuthorizeAsync(PayPalAuthorizeOrderRequest request, CancellationToken cancellationToken)
    {
        var card = request.Card is not null
            ? BuildCardRequest(request.Card)
            : new CardRequest { VaultId = request.VaultId };

        var orderBody = new OrderRequest
        {
            Intent = CheckoutPaymentIntent.Authorize,
            PurchaseUnits = new[]
            {
                new PurchaseUnitRequest
                {
                    Amount = new AmountWithBreakdown
                    {
                        CurrencyCode = request.CurrencyCode,
                        Value = FormatAmount(request.Amount)
                    },
                    CustomId = request.OrderId.ToString(CultureInfo.InvariantCulture),
                    // custom_id is the stable, globally-unique reconciliation key (the order id). PayPal
                    // additionally requires invoice_id to be unique per transaction on this merchant
                    // account, so a nonce derived from the (stable, per-OrderPayment) idempotency key is
                    // appended - retries of the SAME pay attempt reuse it; a new order never collides.
                    InvoiceId = $"ESHOP-{request.OrderId}-{request.PayPalRequestId[..Math.Min(8, request.PayPalRequestId.Length)]}"
                }
            },
            PaymentSource = new PaymentSource { Card = card }
        };

        var order = await ExecuteSettlingWriteAsync(async ct =>
        {
            try
            {
                return await _client.Orders.CreateOrder(
                    new CreateOrderRequest { Body = orderBody, PayPalRequestId = request.PayPalRequestId, Prefer = "return=representation" },
                    cancellationToken: ct).ConfigureAwait(false);
            }
            catch (ApiException<CreateOrderError> ex)
            {
                throw MapTypedError(ex.StatusCode, ex.Error.TryGetError(out var e) ? e : null, ExtractRawFallback(ex.Error), ex);
            }
            catch (ResponseDeserializationException ex)
            {
                throw MapDeserializationError(ex);
            }
            catch (AuthSchemeException ex)
            {
                throw MapAuthSchemeError(ex);
            }
        }, cancellationToken).ConfigureAwait(false);

        if (order.Status == OrderStatus.PayerActionRequired)
        {
            throw new PaymentGatewayException(
                "PayPal requires the shopper to approve this payment in a browser (PAYER_ACTION_REQUIRED). This integration only supports direct card payments with no browser step.",
                PaymentGatewayFailureReason.ChallengeRequired);
        }

        var payPalOrderId = order.Id ?? throw new PaymentGatewayException("PayPal did not return an order id.", PaymentGatewayFailureReason.Unavailable);

        var existingAuthorization = FirstAuthorization(order.PurchaseUnits);
        AuthorizationWithAdditionalData authorization;
        if (existingAuthorization is not null)
        {
            authorization = existingAuthorization;
        }
        else
        {
            var authorizeResponse = await ExecuteSettlingWriteAsync(async ct =>
            {
                try
                {
                    return await _client.Orders.AuthorizeOrder(
                        new AuthorizeOrderRequest { Id = payPalOrderId, PayPalRequestId = request.PayPalRequestId, Prefer = "return=representation" },
                        cancellationToken: ct).ConfigureAwait(false);
                }
                catch (ApiException<AuthorizeOrderError> ex)
                {
                    throw MapTypedError(ex.StatusCode, ex.Error.TryGetError(out var e) ? e : null, ExtractRawFallback(ex.Error), ex);
                }
                catch (ResponseDeserializationException ex)
                {
                    throw MapDeserializationError(ex);
                }
                catch (AuthSchemeException ex)
                {
                    throw MapAuthSchemeError(ex);
                }
            }, cancellationToken).ConfigureAwait(false);

            if (authorizeResponse.Status == OrderStatus.PayerActionRequired)
            {
                throw new PaymentGatewayException(
                    "PayPal requires the shopper to approve this payment in a browser (PAYER_ACTION_REQUIRED). This integration only supports direct card payments with no browser step.",
                    PaymentGatewayFailureReason.ChallengeRequired);
            }

            authorization = FirstAuthorization(authorizeResponse.PurchaseUnits)
                ?? throw new PaymentGatewayException("PayPal did not return an authorization for this order.", PaymentGatewayFailureReason.Unavailable);
        }

        return new PayPalOrderAuthorization(payPalOrderId, ToSnapshot(authorization));
    }

    public async Task<PayPalAuthorizationSnapshot> ReauthorizeAsync(string authorizationId, decimal amount, string currencyCode, string payPalRequestId, CancellationToken cancellationToken)
    {
        var result = await ExecuteSettlingWriteAsync(async ct =>
        {
            try
            {
                return await _client.Payments.ReauthorizePayment(
                    new ReauthorizePaymentRequest
                    {
                        AuthorizationId = authorizationId,
                        Body = new ReauthorizeRequest { Amount = new Money { CurrencyCode = currencyCode, Value = FormatAmount(amount) } },
                        PayPalRequestId = payPalRequestId,
                        Prefer = "return=representation"
                    },
                    cancellationToken: ct).ConfigureAwait(false);
            }
            catch (ApiException<ReauthorizePaymentError> ex)
            {
                var typed = ex.Error.TryGetError(out var e) ? e : null;
                if (typed is not null && IndicatesExpiredAuthorization(typed))
                {
                    throw new PaymentGatewayException(
                        "The payment hold expired and can no longer be renewed (beyond PayPal's reauthorization window). Ask the shopper to pay again.",
                        PaymentGatewayFailureReason.ReauthorizationExpired, typed.DebugId, ex);
                }

                RawError? raw = null;
                if (typed is null)
                {
                    if (ex.Error.TryGetNoContent(out var nc)) raw = nc;
                    else if (ex.Error.TryGetRawError(out var r)) raw = r;
                }
                throw MapTypedError(ex.StatusCode, typed, raw, ex);
            }
            catch (ResponseDeserializationException ex)
            {
                throw MapDeserializationError(ex);
            }
            catch (AuthSchemeException ex)
            {
                throw MapAuthSchemeError(ex);
            }
        }, cancellationToken).ConfigureAwait(false);

        return ToSnapshot(result);
    }

    public async Task<PayPalCaptureResult> CaptureAsync(string authorizationId, string payPalRequestId, CancellationToken cancellationToken)
    {
        var result = await ExecuteSettlingWriteAsync(async ct =>
        {
            try
            {
                return await _client.Payments.CaptureAuthorizedPayment(
                    new CaptureAuthorizedPaymentRequest
                    {
                        AuthorizationId = authorizationId,
                        Body = new CaptureRequest { FinalCapture = true },
                        PayPalRequestId = payPalRequestId,
                        Prefer = "return=representation"
                    },
                    cancellationToken: ct).ConfigureAwait(false);
            }
            catch (ApiException<CaptureAuthorizedPaymentError> ex)
            {
                throw MapTypedErrorWithNoContent(ex.StatusCode, ex.Error.TryGetError(out var e) ? e : null,
                    () => ex.Error.TryGetNoContent(out var nc) ? nc : null,
                    () => ex.Error.TryGetRawError(out var r) ? r : null, ex);
            }
            catch (ResponseDeserializationException ex)
            {
                throw MapDeserializationError(ex);
            }
            catch (AuthSchemeException ex)
            {
                throw MapAuthSchemeError(ex);
            }
        }, cancellationToken).ConfigureAwait(false);

        return ToCaptureResult(result);
    }

    public async Task VoidAsync(string authorizationId, string payPalRequestId, CancellationToken cancellationToken)
    {
        await ExecuteSettlingWriteAsync<object?>(async ct =>
        {
            try
            {
                await _client.Payments.VoidPayment(
                    new VoidPaymentRequest { AuthorizationId = authorizationId, PayPalRequestId = payPalRequestId, Prefer = "return=minimal" },
                    cancellationToken: ct).ConfigureAwait(false);
                return null;
            }
            catch (ApiException<VoidPaymentError> ex)
            {
                throw MapTypedErrorWithNoContent(ex.StatusCode, ex.Error.TryGetError(out var e) ? e : null,
                    () => ex.Error.TryGetNoContent(out var nc) ? nc : null,
                    () => ex.Error.TryGetRawError(out var r) ? r : null, ex);
            }
            catch (ResponseDeserializationException ex) when ((int)ex.StatusCode is >= 200 and < 300)
            {
                // Verified against live sandbox traffic: VoidPayment answers 204 No Content on success,
                // even though its declared return type is PaymentAuthorization - the SDK tries to parse
                // the (empty) body regardless and fails. A 2xx status is the actual signal of success
                // here; we don't need any field off the body (MarkCancelled reads nothing from it).
                return null;
            }
            catch (ResponseDeserializationException ex)
            {
                throw MapDeserializationError(ex);
            }
            catch (AuthSchemeException ex)
            {
                throw MapAuthSchemeError(ex);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PayPalRefundResult> RefundAsync(string captureId, decimal? amount, string currencyCode, string payPalRequestId, CancellationToken cancellationToken)
    {
        var result = await ExecuteSettlingWriteAsync(async ct =>
        {
            try
            {
                return await _client.Payments.RefundCapturedPayment(
                    new RefundCapturedPaymentRequest
                    {
                        CaptureId = captureId,
                        Body = amount is null ? null : new RefundRequest { Amount = new Money { CurrencyCode = currencyCode, Value = FormatAmount(amount.Value) } },
                        PayPalRequestId = payPalRequestId,
                        Prefer = "return=representation"
                    },
                    cancellationToken: ct).ConfigureAwait(false);
            }
            catch (ApiException<RefundCapturedPaymentError> ex)
            {
                throw MapTypedErrorWithNoContent(ex.StatusCode, ex.Error.TryGetError(out var e) ? e : null,
                    () => ex.Error.TryGetNoContent(out var nc) ? nc : null,
                    () => ex.Error.TryGetRawError(out var r) ? r : null, ex);
            }
            catch (ResponseDeserializationException ex)
            {
                throw MapDeserializationError(ex);
            }
            catch (AuthSchemeException ex)
            {
                throw MapAuthSchemeError(ex);
            }
        }, cancellationToken).ConfigureAwait(false);

        return ToRefundResult(result);
    }

    public async Task<PayPalAuthorizationSnapshot> GetAuthorizationAsync(string authorizationId, CancellationToken cancellationToken)
    {
        var result = await BoundedAsync(async ct =>
        {
            try
            {
                return await _client.Payments.GetAuthorizedPayment(
                    new GetAuthorizedPaymentRequest { AuthorizationId = authorizationId }, cancellationToken: ct).ConfigureAwait(false);
            }
            catch (ApiException<GetAuthorizedPaymentError> ex)
            {
                throw MapTypedErrorWithNoContent(ex.StatusCode, ex.Error.TryGetError(out var e) ? e : null,
                    () => ex.Error.TryGetNoContent(out var nc) ? nc : null,
                    () => ex.Error.TryGetRawError(out var r) ? r : null, ex);
            }
            catch (ResponseDeserializationException ex)
            {
                throw MapDeserializationError(ex);
            }
            catch (AuthSchemeException ex)
            {
                throw MapAuthSchemeError(ex);
            }
            catch (SdkConnectionException ex)
            {
                throw new PaymentGatewayException("PayPal did not respond while looking up this authorization.", PaymentGatewayFailureReason.Unavailable, null, ex);
            }
        }, cancellationToken).ConfigureAwait(false);

        return new PayPalAuthorizationSnapshot(
            result.Id ?? authorizationId,
            result.Status?.Value ?? "UNKNOWN",
            result.Amount is null ? 0m : ParseAmount(result.Amount.Value),
            ParseDate(result.ExpirationTime));
    }

    public async Task<PayPalCaptureResult> GetCaptureAsync(string captureId, CancellationToken cancellationToken)
    {
        var result = await BoundedAsync(async ct =>
        {
            try
            {
                return await _client.Payments.GetCapturedPayment(
                    new GetCapturedPaymentRequest { CaptureId = captureId }, cancellationToken: ct).ConfigureAwait(false);
            }
            catch (ApiException<GetCapturedPaymentError> ex)
            {
                throw MapTypedErrorWithNoContent(ex.StatusCode, ex.Error.TryGetError(out var e) ? e : null,
                    () => ex.Error.TryGetNoContent(out var nc) ? nc : null,
                    () => ex.Error.TryGetRawError(out var r) ? r : null, ex);
            }
            catch (ResponseDeserializationException ex)
            {
                throw MapDeserializationError(ex);
            }
            catch (AuthSchemeException ex)
            {
                throw MapAuthSchemeError(ex);
            }
            catch (SdkConnectionException ex)
            {
                throw new PaymentGatewayException("PayPal did not respond while looking up this capture.", PaymentGatewayFailureReason.Unavailable, null, ex);
            }
        }, cancellationToken).ConfigureAwait(false);

        return ToCaptureResult(result);
    }

    public async Task<PayPalRefundResult> GetRefundAsync(string refundId, CancellationToken cancellationToken)
    {
        var result = await BoundedAsync(async ct =>
        {
            try
            {
                return await _client.Payments.GetRefund(
                    new GetRefundRequest { RefundId = refundId }, cancellationToken: ct).ConfigureAwait(false);
            }
            catch (ApiException<GetRefundError> ex)
            {
                throw MapTypedErrorWithNoContent(ex.StatusCode, ex.Error.TryGetError(out var e) ? e : null,
                    () => ex.Error.TryGetNoContent(out var nc) ? nc : null,
                    () => ex.Error.TryGetRawError(out var r) ? r : null, ex);
            }
            catch (ResponseDeserializationException ex)
            {
                throw MapDeserializationError(ex);
            }
            catch (AuthSchemeException ex)
            {
                throw MapAuthSchemeError(ex);
            }
            catch (SdkConnectionException ex)
            {
                throw new PaymentGatewayException("PayPal did not respond while looking up this refund.", PaymentGatewayFailureReason.Unavailable, null, ex);
            }
        }, cancellationToken).ConfigureAwait(false);

        return ToRefundResult(result);
    }

    public async Task<PayPalVaultedCardResult> VaultCardAsync(PayPalVaultCardRequest request, CancellationToken cancellationToken)
    {
        var card = request.Card;
        var tokenRequestCard = new PaymentTokenRequestCard
        {
            Number = card.Number,
            Expiry = card.Expiry,
            SecurityCode = card.SecurityCode,
            Name = card.CardholderName,
            BillingAddress = BuildAddress(card)
        };

        var result = await ExecuteSettlingWriteAsync(async ct =>
        {
            try
            {
                return await _client.Vault.CreatePaymentToken(
                    new CreatePaymentTokenRequest
                    {
                        PayPalRequestId = request.PayPalRequestId,
                        Body = new PaymentTokenRequest
                        {
                            Customer = request.ExistingCustomerId is null ? null : new Customer { Id = request.ExistingCustomerId },
                            PaymentSource = new PaymentTokenRequestPaymentSource { Card = tokenRequestCard }
                        }
                    },
                    cancellationToken: ct).ConfigureAwait(false);
            }
            catch (ApiException<CreatePaymentTokenError> ex)
            {
                throw MapTypedError(ex.StatusCode, ex.Error.TryGetError(out var e) ? e : null, ExtractRawFallback(ex.Error), ex);
            }
            catch (ResponseDeserializationException ex)
            {
                throw MapDeserializationError(ex);
            }
            catch (AuthSchemeException ex)
            {
                throw MapAuthSchemeError(ex);
            }
        }, cancellationToken).ConfigureAwait(false);

        var vaultId = result.Id ?? throw new PaymentGatewayException("PayPal did not return a vault id for this card.", PaymentGatewayFailureReason.Unavailable);
        var cardEntity = result.PaymentSource?.Card;

        return new PayPalVaultedCardResult(
            vaultId,
            result.Customer?.Id,
            cardEntity?.Brand?.Value,
            cardEntity?.LastDigits,
            cardEntity?.Expiry,
            cardEntity?.Name);
    }

    public async Task DeleteVaultedCardAsync(string payPalVaultId, CancellationToken cancellationToken)
    {
        await ExecuteSettlingWriteAsync<object?>(async ct =>
        {
            try
            {
                await _client.Vault.DeletePaymentToken(new DeletePaymentTokenRequest { Id = payPalVaultId }, cancellationToken: ct).ConfigureAwait(false);
                return null;
            }
            catch (ApiException<DeletePaymentTokenError> ex)
            {
                throw MapTypedError(ex.StatusCode, ex.Error.TryGetError(out var e) ? e : null, ExtractRawFallback(ex.Error), ex);
            }
            catch (ResponseDeserializationException ex)
            {
                throw MapDeserializationError(ex);
            }
            catch (AuthSchemeException ex)
            {
                throw MapAuthSchemeError(ex);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PayPalTransactionSearchResult> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var transactions = new List<PayPalTransactionRecord>();
        var complete = true;
        var startDate = FormatDate(from);
        var endDate = FormatDate(to);

        for (var page = 1; page <= MaxTransactionSearchPages; page++)
        {
            var response = await BoundedAsync(async ct =>
            {
                try
                {
                    return await _client.TransactionSearch.SearchTransactions(
                        new SearchTransactionsRequest
                        {
                            StartDate = startDate,
                            EndDate = endDate,
                            Fields = "all",
                            PageSize = 500,
                            Page = page
                        },
                        cancellationToken: ct).ConfigureAwait(false);
                }
                catch (ApiException<RawError> ex)
                {
                    throw new PaymentGatewayException(
                        $"PayPal transaction search returned HTTP {(int)ex.StatusCode}: {Truncate(ex.Error.ReadAsString())}",
                        ReasonForStatus(ex.StatusCode), null, ex);
                }
                catch (ResponseDeserializationException ex)
                {
                    throw MapDeserializationError(ex);
                }
                catch (AuthSchemeException ex)
                {
                    throw MapAuthSchemeError(ex);
                }
                catch (SdkConnectionException ex)
                {
                    throw new PaymentGatewayException("PayPal did not respond to the transaction search.", PaymentGatewayFailureReason.Unavailable, null, ex);
                }
            }, cancellationToken).ConfigureAwait(false);

            foreach (var detail in response.TransactionDetails ?? Array.Empty<TransactionDetails>())
            {
                var info = detail.TransactionInfo;
                if (info is null) continue;

                transactions.Add(new PayPalTransactionRecord(
                    info.TransactionId ?? string.Empty,
                    info.TransactionAmount is null ? null : ParseAmount(info.TransactionAmount.Value),
                    info.TransactionAmount?.CurrencyCode,
                    info.TransactionStatus,
                    info.InvoiceId,
                    info.CustomField,
                    ParseDate(info.TransactionInitiationDate),
                    info.FeeAmount is null ? null : ParseAmount(info.FeeAmount.Value)));
            }

            var totalPages = response.TotalPages ?? 1;
            if (page >= totalPages)
            {
                break;
            }

            if (page == MaxTransactionSearchPages)
            {
                complete = false;
            }
        }

        return new PayPalTransactionSearchResult(transactions, complete);
    }

    // ---- helpers -------------------------------------------------------

    private static CardRequest BuildCardRequest(PayPalCardInput card) => new()
    {
        Number = card.Number,
        Expiry = card.Expiry,
        SecurityCode = card.SecurityCode,
        Name = card.CardholderName,
        BillingAddress = BuildAddress(card)
    };

    private static Address? BuildAddress(PayPalCardInput card)
    {
        if (string.IsNullOrWhiteSpace(card.CountryCode))
        {
            return null;
        }

        return new Address
        {
            AddressLine1 = card.AddressLine1,
            AddressLine2 = card.AddressLine2,
            AdminArea1 = card.AdminArea1,
            AdminArea2 = card.AdminArea2,
            PostalCode = card.PostalCode,
            CountryCode = card.CountryCode
        };
    }

    private static AuthorizationWithAdditionalData? FirstAuthorization(IReadOnlyList<PurchaseUnit>? units)
    {
        if (units is null || units.Count == 0) return null;
        var authorizations = units[0].Payments?.Authorizations;
        return authorizations is { Count: > 0 } ? authorizations[0] : null;
    }

    private static PayPalAuthorizationSnapshot ToSnapshot(AuthorizationWithAdditionalData authorization) => new(
        authorization.Id ?? throw new PaymentGatewayException("PayPal did not return an authorization id.", PaymentGatewayFailureReason.Unavailable),
        authorization.Status?.Value ?? "UNKNOWN",
        authorization.Amount is null ? 0m : ParseAmount(authorization.Amount.Value),
        ParseDate(authorization.ExpirationTime));

    private static PayPalAuthorizationSnapshot ToSnapshot(PaymentAuthorization authorization) => new(
        authorization.Id ?? throw new PaymentGatewayException("PayPal did not return an authorization id.", PaymentGatewayFailureReason.Unavailable),
        authorization.Status?.Value ?? "UNKNOWN",
        authorization.Amount is null ? 0m : ParseAmount(authorization.Amount.Value),
        ParseDate(authorization.ExpirationTime));

    private static PayPalCaptureResult ToCaptureResult(CapturedPayment capture)
    {
        var captureId = capture.Id ?? throw new PaymentGatewayException("PayPal did not return a capture id.", PaymentGatewayFailureReason.Unavailable);
        var breakdown = capture.SellerReceivableBreakdown;
        var gross = breakdown is not null ? ParseAmount(breakdown.GrossAmount.Value)
            : (capture.Amount is not null ? ParseAmount(capture.Amount.Value) : 0m);
        return new PayPalCaptureResult(
            captureId,
            capture.Status?.Value ?? "UNKNOWN",
            gross,
            breakdown?.PaypalFee is null ? null : ParseAmount(breakdown.PaypalFee.Value),
            breakdown?.NetAmount is null ? null : ParseAmount(breakdown.NetAmount.Value));
    }

    private static PayPalRefundResult ToRefundResult(Refund refund) => new(
        refund.Id ?? throw new PaymentGatewayException("PayPal did not return a refund id.", PaymentGatewayFailureReason.Unavailable),
        refund.Status?.Value ?? "UNKNOWN",
        refund.Amount is null ? 0m : ParseAmount(refund.Amount.Value));

    private static string FormatAmount(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static decimal ParseAmount(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    private static DateTimeOffset? ParseDate(string? value) =>
        string.IsNullOrEmpty(value) ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string FormatDate(DateTimeOffset value) => value.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    private static string Truncate(string value) => value.Length <= 500 ? value : value[..500];

    private static RawError? ExtractRawFallback(ApiError error) => error.TryGetRawError(out var raw) ? raw : null;

    /// <summary>
    /// The exact shape PayPal uses to signal "this authorization is past its reauthorization window" is
    /// UNVERIFIED without live traffic (see PLAN.md). Defensive heuristic: a fine-grained issue code
    /// (the documented place for this - Error.Details[].Issue) or, failing that, the top-level error name
    /// or message, mentioning "expir" next to "authoriz".
    /// </summary>
    private static bool IndicatesExpiredAuthorization(Error error)
    {
        bool MentionsExpiry(string? text) =>
            text is not null && text.Contains("EXPIR", StringComparison.OrdinalIgnoreCase);

        if (error.Details is not null && error.Details.Any(d => MentionsExpiry(d.Issue) || MentionsExpiry(d.Description)))
        {
            return true;
        }

        return MentionsExpiry(error.Name) || MentionsExpiry(error.Message);
    }

    private static PaymentGatewayFailureReason ReasonForStatus(HttpStatusCode status)
    {
        var code = (int)status;
        if (code is 401 or 403 or 429) return PaymentGatewayFailureReason.Unavailable;
        if (code is >= 400 and < 500) return PaymentGatewayFailureReason.ProviderRejected;
        return PaymentGatewayFailureReason.Unavailable;
    }

    private static PaymentGatewayException MapTypedError(HttpStatusCode statusCode, Error? typed, RawError? raw, Exception inner)
    {
        if (typed is not null)
        {
            return new PaymentGatewayException(DescribeError(typed), ReasonForStatus(statusCode), typed.DebugId, inner);
        }
        if (raw is not null)
        {
            return new PaymentGatewayException($"PayPal returned HTTP {(int)raw.StatusCode}: {Truncate(raw.ReadAsString())}", ReasonForStatus(raw.StatusCode), null, inner);
        }
        return new PaymentGatewayException($"PayPal returned HTTP {(int)statusCode} with an unrecognised error shape.", PaymentGatewayFailureReason.Unavailable, null, inner);
    }

    /// <summary>The top-level message is often a generic bucket ("semantically incorrect..."); the fine-grained, operator-actionable reason is in Details[].Issue.</summary>
    private static string DescribeError(Error error)
    {
        if (error.Details is null || error.Details.Count == 0)
        {
            return error.Message;
        }

        var issues = string.Join("; ", error.Details.Select(d => d.Description is null ? d.Issue : $"{d.Issue} ({d.Description})"));
        return $"{error.Message} [{issues}]";
    }

    private static PaymentGatewayException MapTypedErrorWithNoContent(HttpStatusCode statusCode, Error? typed, Func<RawError?> noContent, Func<RawError?> rawFallback, Exception inner)
    {
        if (typed is not null)
        {
            return new PaymentGatewayException(DescribeError(typed), ReasonForStatus(statusCode), typed.DebugId, inner);
        }
        var noContentRaw = noContent();
        if (noContentRaw is not null)
        {
            return new PaymentGatewayException($"PayPal returned HTTP {(int)noContentRaw.StatusCode} with no error detail.", ReasonForStatus(noContentRaw.StatusCode), null, inner);
        }
        return MapTypedError(statusCode, null, rawFallback(), inner);
    }

    private static PaymentGatewayException MapDeserializationError(ResponseDeserializationException ex)
    {
        var code = (int)ex.StatusCode;
        var reason = code is >= 200 and < 300 ? PaymentGatewayFailureReason.Unavailable : ReasonForStatus(ex.StatusCode);
        return new PaymentGatewayException("PayPal returned a response that could not be processed.", reason, null, ex);
    }

    private static PaymentGatewayException MapAuthSchemeError(AuthSchemeException ex) =>
        new("PayPal rejected our credentials.", PaymentGatewayFailureReason.Unavailable, null, ex);

    private static async Task<T> BoundedAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(CallBudget);
        return await operation(cts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Bounds a write and, if the connection itself fails (outcome unknown - the bytes may have reached
    /// PayPal), retries exactly once using the same call (and therefore the same PayPal-Request-Id) to
    /// settle the outcome before giving up. A real API error (ApiException) is never retried here.
    /// </summary>
    private static async Task<T> ExecuteSettlingWriteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        return await BoundedAsync(async ct =>
        {
            try
            {
                return await operation(ct).ConfigureAwait(false);
            }
            catch (SdkConnectionException)
            {
                try
                {
                    return await operation(ct).ConfigureAwait(false);
                }
                catch (SdkConnectionException ex2)
                {
                    throw new PaymentGatewayException(
                        "PayPal did not respond to this request. Whether it was applied is unknown and must be reconciled before retrying.",
                        PaymentGatewayFailureReason.Unavailable, null, ex2);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}
