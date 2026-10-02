using System;
using System.Globalization;
using System.Linq;
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
using PayPalOrderStatus = PayPalServerSdk.Models.Enums.OrderStatus;
using PayPalOrder = PayPalServerSdk.Models.Order;
using PayPalRefund = PayPalServerSdk.Models.Refund;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

public class PayPalPaymentGateway : IPayPalPaymentGateway
{
    private readonly PayPalServerSdkClient _client;

    public PayPalPaymentGateway(PayPalServerSdkClient client)
    {
        _client = client;
    }

    public async Task<AuthorizeResult> AuthorizeAsync(AuthorizeRequest request, CancellationToken cancellationToken)
    {
        var card = request.SavedCardVaultId is not null
            ? new CardRequest { VaultId = request.SavedCardVaultId }
            : BuildCardRequest(request.Card!);

        var orderRequest = new OrderRequest
        {
            Intent = CheckoutPaymentIntent.Authorize,
            PurchaseUnits =
            [
                new PurchaseUnitRequest
                {
                    Amount = new AmountWithBreakdown
                    {
                        CurrencyCode = request.Currency,
                        Value = FormatAmount(request.Amount)
                    },
                    // Reported back verbatim on the capture's transaction-search record (TransactionInformation.InvoiceId) —
                    // the reconciliation correlation key, since PayPal's own transaction id is NOT the capture id.
                    // Reusing the (already globally-unique, guid-salted) idempotency key also satisfies
                    // PayPal's own per-merchant-account invoice_id uniqueness requirement.
                    InvoiceId = request.IdempotencyKey
                }
            ],
            PaymentSource = new PaymentSource { Card = card }
        };

        var createRequest = new CreateOrderRequest
        {
            Body = orderRequest,
            PayPalRequestId = IdempotencyKeyFor(request.IdempotencyKey)
        };

        try
        {
            PayPalOrder order = await ExecuteWithConnectionRetryAsync(
                () => _client.Orders.CreateOrder(createRequest, cancellationToken: cancellationToken));

            if (order.Status == PayPalOrderStatus.PayerActionRequired)
            {
                throw new PaymentRequiresBrowserApprovalException(order.Id ?? "(unknown)");
            }

            var authorization = order.PurchaseUnits?.FirstOrDefault()?.Payments?.Authorizations?.FirstOrDefault();
            if (order.Status != PayPalOrderStatus.Completed || authorization?.Id is null)
            {
                return new AuthorizeResult(false, false, order.Id, null, order.Status?.Value, null, null,
                    $"PayPal order {order.Id} did not authorize; status was {order.Status?.Value ?? "unknown"}.");
            }

            var cardResponse = order.PaymentSource?.Card;
            return new AuthorizeResult(true, false, order.Id, authorization.Id, authorization.Status?.Value,
                cardResponse?.Brand?.Value, cardResponse?.LastDigits, null);
        }
        catch (ApiException<CreateOrderError> ex)
        {
            return new AuthorizeResult(false, false, null, null, null, null, null, DescribeError(ex.Error, ex.StatusCode));
        }
        catch (ResponseDeserializationException ex)
        {
            throw new PaymentGatewayException("PayPal returned a response that could not be processed while authorizing payment.", ex);
        }
    }

    public async Task<CaptureResult> CaptureAsync(string authorizationId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var request = new CaptureAuthorizedPaymentRequest
        {
            AuthorizationId = authorizationId,
            PayPalRequestId = IdempotencyKeyFor(idempotencyKey),
            Prefer = "return=representation"
        };

        try
        {
            CapturedPayment capture = await ExecuteWithConnectionRetryAsync(
                () => _client.Payments.CaptureAuthorizedPayment(request, cancellationToken: cancellationToken));

            var breakdown = capture.SellerReceivableBreakdown;
            return new CaptureResult(true, false, capture.Id, capture.Status?.Value,
                ParseMoney(capture.Amount) ?? 0m, ParseMoney(breakdown?.PaypalFee), ParseMoney(breakdown?.NetAmount), null);
        }
        catch (ApiException<CaptureAuthorizedPaymentError> ex)
        {
            // Treated generically: a capture on a non-stale authorization that is genuinely declined
            // will also fail reauthorize+retry and surface the same operator-actionable error.
            return new CaptureResult(false, true, null, null, 0m, null, null, DescribeError(ex.Error, ex.StatusCode));
        }
        catch (ResponseDeserializationException ex)
        {
            throw new PaymentGatewayException("PayPal returned a response that could not be processed while capturing payment.", ex);
        }
    }

    public async Task<ReauthorizeResult> ReauthorizeAsync(string authorizationId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var request = new ReauthorizePaymentRequest
        {
            AuthorizationId = authorizationId,
            PayPalRequestId = IdempotencyKeyFor(idempotencyKey)
        };

        try
        {
            PaymentAuthorization result = await ExecuteWithConnectionRetryAsync(
                () => _client.Payments.ReauthorizePayment(request, cancellationToken: cancellationToken));

            return new ReauthorizeResult(true, result.Id, result.Status?.Value, null);
        }
        catch (ApiException<ReauthorizePaymentError> ex)
        {
            return new ReauthorizeResult(false, null, null, DescribeError(ex.Error, ex.StatusCode));
        }
        catch (ResponseDeserializationException ex)
        {
            throw new PaymentGatewayException("PayPal returned a response that could not be processed while reauthorizing payment.", ex);
        }
    }

    public async Task VoidAsync(string authorizationId, CancellationToken cancellationToken)
    {
        var request = new VoidPaymentRequest { AuthorizationId = authorizationId };

        try
        {
            await ExecuteWithConnectionRetryAsync(
                () => _client.Payments.VoidPayment(request, cancellationToken: cancellationToken));
        }
        catch (ApiException<VoidPaymentError> ex)
        {
            throw new PaymentGatewayException($"PayPal could not void authorization {authorizationId}: {DescribeError(ex.Error, ex.StatusCode)}", ex);
        }
        catch (ResponseDeserializationException ex) when ((int)ex.StatusCode is >= 200 and < 300)
        {
            // SDK gap (not an application workaround): PayPal's void-authorization endpoint always
            // answers 204 No Content on success, but the generated VoidPayment method declares its
            // return type as Task<PaymentAuthorization> and unconditionally tries to deserialize the
            // (empty) body — so a genuine success surfaces as this exception. Confirmed directly
            // against the sandbox: the authorization's status was VOIDED even though this line threw.
            // A non-2xx status here is a real deserialization failure and still propagates below.
        }
        catch (ResponseDeserializationException ex)
        {
            throw new PaymentGatewayException("PayPal returned a response that could not be processed while voiding payment.", ex);
        }
    }

    public async Task<RefundResult> RefundAsync(RefundGatewayRequest request, CancellationToken cancellationToken)
    {
        var refundRequest = new RefundCapturedPaymentRequest
        {
            CaptureId = request.CaptureId,
            PayPalRequestId = IdempotencyKeyFor(request.IdempotencyKey),
            Prefer = "return=representation",
            Body = new RefundRequest
            {
                Amount = request.Amount.HasValue
                    ? new Money { CurrencyCode = request.Currency, Value = FormatAmount(request.Amount.Value) }
                    : null,
                NoteToPayer = request.Note
            }
        };

        try
        {
            PayPalRefund refund = await ExecuteWithConnectionRetryAsync(
                () => _client.Payments.RefundCapturedPayment(refundRequest, cancellationToken: cancellationToken));

            return new RefundResult(true, refund.Id, refund.Status?.Value, null);
        }
        catch (ApiException<RefundCapturedPaymentError> ex)
        {
            return new RefundResult(false, null, null, DescribeError(ex.Error, ex.StatusCode));
        }
        catch (ResponseDeserializationException ex)
        {
            throw new PaymentGatewayException("PayPal returned a response that could not be processed while refunding payment.", ex);
        }
    }

    public async Task<SavedCardResult> SaveCardAsync(SaveCardRequest request, CancellationToken cancellationToken)
    {
        var tokenRequest = new CreatePaymentTokenRequest
        {
            PayPalRequestId = IdempotencyKeyFor(request.IdempotencyKey),
            Body = new PaymentTokenRequest
            {
                Customer = new Customer { MerchantCustomerId = request.BuyerId },
                PaymentSource = new PaymentTokenRequestPaymentSource
                {
                    Card = new PaymentTokenRequestCard
                    {
                        Name = request.Card.CardholderName,
                        Number = request.Card.Number,
                        Expiry = request.Card.ExpiryYearMonth,
                        SecurityCode = request.Card.SecurityCode,
                        BillingAddress = BuildAddress(request.Card)
                    }
                }
            }
        };

        try
        {
            PaymentTokenResponse token = await ExecuteWithConnectionRetryAsync(
                () => _client.Vault.CreatePaymentToken(tokenRequest, cancellationToken: cancellationToken));

            var cardResponse = token.PaymentSource?.Card;
            return new SavedCardResult(true, token.Id, cardResponse?.Brand?.Value, cardResponse?.LastDigits,
                cardResponse?.Expiry, null);
        }
        catch (ApiException<CreatePaymentTokenError> ex)
        {
            return new SavedCardResult(false, null, null, null, null, DescribeError(ex.Error, ex.StatusCode));
        }
        catch (ResponseDeserializationException ex)
        {
            throw new PaymentGatewayException("PayPal returned a response that could not be processed while saving the card.", ex);
        }
    }

    public async Task DeleteSavedCardAsync(string payPalVaultId, CancellationToken cancellationToken)
    {
        var request = new DeletePaymentTokenRequest { Id = payPalVaultId };

        try
        {
            await ExecuteWithConnectionRetryAsync(
                () => _client.Vault.DeletePaymentToken(request, cancellationToken: cancellationToken));
        }
        catch (ApiException<DeletePaymentTokenError>)
        {
            // Best-effort: our own PaymentMethod row is the authoritative record of what the shopper can
            // use. If PayPal has already forgotten the token (or rejects the delete), the card is still
            // unusable to pay once removed from our own store, so this is not escalated.
        }
        catch (ResponseDeserializationException ex)
        {
            throw new PaymentGatewayException("PayPal returned a response that could not be processed while deleting the saved card.", ex);
        }
    }

    public async Task<ReconciliationResult> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        const int MaxPages = 100;
        var transactions = new System.Collections.Generic.List<ReconciliationTransaction>();
        int page = 1;
        int? totalPages = null;
        bool truncated = false;

        try
        {
            while (true)
            {
                var request = new SearchTransactionsRequest
                {
                    StartDate = from.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
                    EndDate = to.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
                    Page = page
                };

                SearchResponse response = await ExecuteWithConnectionRetryAsync(
                    () => _client.TransactionSearch.SearchTransactions(request, cancellationToken: cancellationToken));

                totalPages = response.TotalPages;

                foreach (var detail in response.TransactionDetails ?? [])
                {
                    var info = detail.TransactionInfo;
                    if (info?.TransactionId is null)
                    {
                        continue;
                    }

                    DateTimeOffset? initiatedAt = DateTimeOffset.TryParse(info.TransactionInitiationDate,
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                        ? parsed
                        : null;

                    transactions.Add(new ReconciliationTransaction(
                        info.TransactionId,
                        ParseMoney(info.TransactionAmount) ?? 0m,
                        info.TransactionAmount?.CurrencyCode ?? string.Empty,
                        initiatedAt,
                        info.InvoiceId));
                }

                if (totalPages is null || page >= totalPages.Value)
                {
                    break;
                }

                if (page >= MaxPages)
                {
                    truncated = true;
                    break;
                }

                page++;
            }
        }
        catch (ApiException<RawError> ex)
        {
            throw new PaymentGatewayException($"PayPal transaction search failed with HTTP {(int)ex.StatusCode}: {ex.Error.ReadAsString()}", ex);
        }
        catch (ResponseDeserializationException ex)
        {
            throw new PaymentGatewayException("PayPal returned a response that could not be processed during reconciliation.", ex);
        }

        return new ReconciliationResult(transactions, truncated, page, totalPages);
    }

    private static CardRequest BuildCardRequest(CardDetails card) => new()
    {
        Name = card.CardholderName,
        Number = card.Number,
        Expiry = card.ExpiryYearMonth,
        SecurityCode = card.SecurityCode,
        BillingAddress = BuildAddress(card)
    };

    private static Address? BuildAddress(CardDetails card)
    {
        if (card.CountryCode is null)
        {
            return null;
        }

        return new Address
        {
            AddressLine1 = card.AddressLine1,
            AdminArea2 = card.City,
            AdminArea1 = card.State,
            PostalCode = card.PostalCode,
            CountryCode = card.CountryCode
        };
    }

    private static string FormatAmount(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

    private static decimal? ParseMoney(Money? money) =>
        money is null ? null : decimal.Parse(money.Value, NumberStyles.Number, CultureInfo.InvariantCulture);

    /// <summary>
    /// PayPal's own PayPalRequestId dedup window means a resend after a connection failure is the
    /// reconciliation path for these calls: it is safe to try exactly once more with the identical key.
    /// </summary>
    private static async Task<T> ExecuteWithConnectionRetryAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (SdkConnectionException)
        {
            try
            {
                return await action();
            }
            catch (SdkConnectionException ex)
            {
                throw new PaymentGatewayException(
                    "Could not reach PayPal; the outcome of the attempted operation is unknown. It is safe to retry with the same request.",
                    ex);
            }
        }
        catch (AuthSchemeException ex)
        {
            throw new PaymentGatewayException("PayPal rejected this application's credentials.", ex);
        }
    }

    private static async Task ExecuteWithConnectionRetryAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (SdkConnectionException)
        {
            try
            {
                await action();
            }
            catch (SdkConnectionException ex)
            {
                throw new PaymentGatewayException(
                    "Could not reach PayPal; the outcome of the attempted operation is unknown. It is safe to retry with the same request.",
                    ex);
            }
        }
        catch (AuthSchemeException ex)
        {
            throw new PaymentGatewayException("PayPal rejected this application's credentials.", ex);
        }
    }

    private static string IdempotencyKeyFor(string key) => key;

    private static string DescribeError(PayPalServerSdk.Core.ErrorResponse.ApiError error, System.Net.HttpStatusCode statusCode)
    {
        if (error.TryGetRawError(out var raw))
        {
            return $"HTTP {(int)raw.StatusCode}: {raw.ReadAsString()}";
        }

        return $"HTTP {(int)statusCode}";
    }

    private static string DescribeError(CreateOrderError error, System.Net.HttpStatusCode statusCode) =>
        error.TryGetError(out var e) ? $"{e.Name}: {e.Message}" : DescribeError((PayPalServerSdk.Core.ErrorResponse.ApiError)error, statusCode);

    private static string DescribeError(CaptureAuthorizedPaymentError error, System.Net.HttpStatusCode statusCode)
    {
        if (error.TryGetError(out var e))
        {
            return $"{e.Name}: {e.Message}";
        }
        if (error.TryGetNoContent(out var raw))
        {
            return $"HTTP {(int)raw.StatusCode}";
        }
        return DescribeError((PayPalServerSdk.Core.ErrorResponse.ApiError)error, statusCode);
    }

    private static string DescribeError(ReauthorizePaymentError error, System.Net.HttpStatusCode statusCode)
    {
        if (error.TryGetError(out var e))
        {
            return $"{e.Name}: {e.Message}";
        }
        if (error.TryGetNoContent(out var raw))
        {
            return $"HTTP {(int)raw.StatusCode}";
        }
        return DescribeError((PayPalServerSdk.Core.ErrorResponse.ApiError)error, statusCode);
    }

    private static string DescribeError(VoidPaymentError error, System.Net.HttpStatusCode statusCode)
    {
        if (error.TryGetError(out var e))
        {
            return $"{e.Name}: {e.Message}";
        }
        if (error.TryGetNoContent(out var raw))
        {
            return $"HTTP {(int)raw.StatusCode}";
        }
        return DescribeError((PayPalServerSdk.Core.ErrorResponse.ApiError)error, statusCode);
    }

    private static string DescribeError(RefundCapturedPaymentError error, System.Net.HttpStatusCode statusCode)
    {
        if (error.TryGetError(out var e))
        {
            return $"{e.Name}: {e.Message}";
        }
        if (error.TryGetNoContent(out var raw))
        {
            return $"HTTP {(int)raw.StatusCode}";
        }
        return DescribeError((PayPalServerSdk.Core.ErrorResponse.ApiError)error, statusCode);
    }

    private static string DescribeError(CreatePaymentTokenError error, System.Net.HttpStatusCode statusCode) =>
        error.TryGetError(out var e) ? $"{e.Name}: {e.Message}" : DescribeError((PayPalServerSdk.Core.ErrorResponse.ApiError)error, statusCode);
}
