using System;
using System.Collections.Generic;
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

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// The sole boundary over PayPal. Owns the SDK client, the error translation, and the idempotency
/// keys on the wire. Every SDK failure is translated into a <see cref="PaymentGatewayException"/> so
/// the application has one failure type to handle.
/// </summary>
public sealed class PayPalPaymentGateway : IPaymentGateway
{
    private static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan SearchBudget = TimeSpan.FromSeconds(120);
    private const int MaxPagesPerWindow = 50;   // 50 × 500 = up to 25,000 transactions per 31-day window
    private const string Representation = "return=representation";

    private readonly PayPalServerSdkClient _client;
    private readonly IAppLogger<PayPalPaymentGateway> _logger;
    private readonly string _currency;

    public PayPalPaymentGateway(PayPalServerSdkClient client, PayPalSettings settings, IAppLogger<PayPalPaymentGateway> logger)
    {
        _client = client;
        _logger = logger;
        _currency = string.IsNullOrWhiteSpace(settings.Currency) ? "USD" : settings.Currency!.Trim().ToUpperInvariant();
    }

    public string Currency => _currency;

    // --- Authorize -----------------------------------------------------------------------------

    public async Task<AuthorizationResult> AuthorizeAsync(AuthorizeGatewayRequest request, CancellationToken cancellationToken = default)
    {
        using var cts = Budget(cancellationToken, CallBudget);
        var token = cts.Token;

        var purchaseUnit = new PurchaseUnitRequest
        {
            Amount = new AmountWithBreakdown { CurrencyCode = _currency, Value = FormatAmount(request.Amount) },
            CustomId = request.OrderId.ToString(CultureInfo.InvariantCulture),
            InvoiceId = request.InvoiceId,
            Description = request.Description
        };

        var orderRequest = new OrderRequest
        {
            Intent = CheckoutPaymentIntent.Authorize,
            PurchaseUnits = new[] { purchaseUnit },
            PaymentSource = new PaymentSource { Card = BuildCard(request) }
        };

        Order order;
        try
        {
            order = await _client.Orders.CreateOrder(new CreateOrderRequest
            {
                Body = orderRequest,
                PayPalRequestId = request.CreateIdempotencyKey,
                Prefer = Representation
            }, cancellationToken: token);
        }
        catch (ApiException<CreateOrderError> ex)
        {
            throw TranslateTyped(ex, "create order", ex.Error.TryGetError);
        }
        catch (Exception ex) when (TryTranslateInfra(ex, isWrite: true, "create order", out var translated))
        {
            throw translated;
        }

        EnsureNoChallenge(order.Status, order.Links, order.PurchaseUnits is null ? null : FindAuthorization(order.PurchaseUnits), "order creation");

        var auth = FindAuthorization(order.PurchaseUnits);
        var orderId = order.Id ?? throw new PaymentGatewayException("PayPal did not return an order id.", null, false);

        if (auth is null && order.Status == OrderStatus.Approved)
        {
            // Card approved but not yet authorized: create the authorization explicitly.
            OrderAuthorizeResponse authResponse;
            try
            {
                authResponse = await _client.Orders.AuthorizeOrder(new AuthorizeOrderRequest
                {
                    Id = orderId,
                    PayPalRequestId = request.AuthorizeIdempotencyKey,
                    Prefer = Representation
                }, cancellationToken: token);
            }
            catch (ApiException<AuthorizeOrderError> ex)
            {
                throw TranslateTyped(ex, "authorize order", ex.Error.TryGetError);
            }
            catch (Exception ex) when (TryTranslateInfra(ex, isWrite: true, "authorize order", out var translated))
            {
                throw translated;
            }

            EnsureNoChallenge(authResponse.Status, authResponse.Links, FindAuthorization(authResponse.PurchaseUnits), "order authorization");
            auth = FindAuthorization(authResponse.PurchaseUnits);
        }

        if (auth?.Id is null)
        {
            throw new PaymentGatewayException(
                $"PayPal did not return an authorization for order {orderId} (status {order.Status?.Value}).", null, false);
        }
        if (auth.Status == AuthorizationStatus.Denied)
        {
            throw new PaymentGatewayException("The card authorization was denied by the issuer.", 402, isCallerError: true, payPalIssue: "DENIED");
        }

        return new AuthorizationResult(orderId, auth.Id, auth.Status?.Value, ParseDate(auth.ExpirationTime), request.Amount);
    }

    public async Task<AuthorizationSnapshot> GetAuthorizationAsync(string authorizationId, CancellationToken cancellationToken = default)
    {
        using var cts = Budget(cancellationToken, CallBudget);
        PaymentAuthorization auth;
        try
        {
            auth = await _client.Payments.GetAuthorizedPayment(
                new GetAuthorizedPaymentRequest { AuthorizationId = authorizationId }, cancellationToken: cts.Token);
        }
        catch (ApiException<GetAuthorizedPaymentError> ex)
        {
            throw TranslateTyped(ex, "get authorization", ex.Error.TryGetError);
        }
        catch (Exception ex) when (TryTranslateInfra(ex, isWrite: false, "get authorization", out var translated))
        {
            throw translated;
        }

        return new AuthorizationSnapshot(auth.Id ?? authorizationId, auth.Status?.Value, ParseDate(auth.ExpirationTime));
    }

    public async Task<AuthorizationSnapshot> ReauthorizeAsync(string authorizationId, decimal amount, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        using var cts = Budget(cancellationToken, CallBudget);
        PaymentAuthorization auth;
        try
        {
            auth = await _client.Payments.ReauthorizePayment(new ReauthorizePaymentRequest
            {
                AuthorizationId = authorizationId,
                PayPalRequestId = idempotencyKey,
                Body = new ReauthorizeRequest { Amount = new Money { CurrencyCode = _currency, Value = FormatAmount(amount) } }
            }, cancellationToken: cts.Token);
        }
        catch (ApiException<ReauthorizePaymentError> ex)
        {
            throw TranslateTyped(ex, "reauthorize", ex.Error.TryGetError);
        }
        catch (Exception ex) when (TryTranslateInfra(ex, isWrite: true, "reauthorize", out var translated))
        {
            throw translated;
        }

        return new AuthorizationSnapshot(auth.Id ?? authorizationId, auth.Status?.Value, ParseDate(auth.ExpirationTime));
    }

    // --- Capture / Void / Refund ---------------------------------------------------------------

    public async Task<CaptureResult> CaptureAsync(string authorizationId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        using var cts = Budget(cancellationToken, CallBudget);
        CapturedPayment capture;
        try
        {
            capture = await _client.Payments.CaptureAuthorizedPayment(new CaptureAuthorizedPaymentRequest
            {
                AuthorizationId = authorizationId,
                PayPalRequestId = idempotencyKey,
                Prefer = Representation
            }, cancellationToken: cts.Token);
        }
        catch (ApiException<CaptureAuthorizedPaymentError> ex)
        {
            throw TranslateTyped(ex, "capture", ex.Error.TryGetError);
        }
        catch (Exception ex) when (TryTranslateInfra(ex, isWrite: true, "capture", out var translated))
        {
            throw translated;
        }

        var breakdown = capture.SellerReceivableBreakdown;
        var gross = ParseMoney(breakdown?.GrossAmount) ?? ParseMoney(capture.Amount)
            ?? throw new PaymentGatewayException("PayPal capture returned no amount.", null, false);
        var captureId = capture.Id ?? throw new PaymentGatewayException("PayPal capture returned no id.", null, false);
        var currency = breakdown?.GrossAmount?.CurrencyCode ?? capture.Amount?.CurrencyCode ?? _currency;

        return new CaptureResult(captureId, capture.Status?.Value, gross, ParseMoney(breakdown?.PaypalFee), ParseMoney(breakdown?.NetAmount), currency);
    }

    public async Task VoidAsync(string authorizationId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        using var cts = Budget(cancellationToken, CallBudget);
        try
        {
            await _client.Payments.VoidPayment(new VoidPaymentRequest
            {
                AuthorizationId = authorizationId,
                PayPalRequestId = idempotencyKey
            }, cancellationToken: cts.Token);
        }
        catch (ResponseDeserializationException ex) when ((int)ex.StatusCode is >= 200 and < 300)
        {
            // A successful void returns 204 No Content; the SDK cannot deserialize the empty body into a
            // PaymentAuthorization. A 2xx here means the authorization was voided — treat it as success.
        }
        catch (ApiException<VoidPaymentError> ex)
        {
            throw TranslateTyped(ex, "void authorization", ex.Error.TryGetError);
        }
        catch (Exception ex) when (TryTranslateInfra(ex, isWrite: true, "void authorization", out var translated))
        {
            throw translated;
        }
    }

    public async Task<RefundResult> RefundAsync(string captureId, decimal? amount, string idempotencyKey, string? invoiceId, CancellationToken cancellationToken = default)
    {
        using var cts = Budget(cancellationToken, CallBudget);
        // Amount set => partial refund; omitted => full remaining refund. We deliberately do not send
        // invoice_id on the refund to avoid PayPal's per-merchant invoice-uniqueness checks.
        RefundRequest? body = amount.HasValue
            ? new RefundRequest { Amount = new Money { CurrencyCode = _currency, Value = FormatAmount(amount.Value) } }
            : null;

        Refund refund;
        try
        {
            refund = await _client.Payments.RefundCapturedPayment(new RefundCapturedPaymentRequest
            {
                CaptureId = captureId,
                PayPalRequestId = idempotencyKey,   // caller-supplied idempotency key
                Prefer = Representation,
                Body = body
            }, cancellationToken: cts.Token);
        }
        catch (ApiException<RefundCapturedPaymentError> ex)
        {
            throw TranslateTyped(ex, "refund", ex.Error.TryGetError);
        }
        catch (Exception ex) when (TryTranslateInfra(ex, isWrite: true, "refund", out var translated))
        {
            throw translated;
        }

        var refundId = refund.Id ?? throw new PaymentGatewayException("PayPal refund returned no id.", null, false);
        var refundAmount = ParseMoney(refund.Amount) ?? amount ?? 0m;
        return new RefundResult(refundId, refund.Status?.Value, refundAmount);
    }

    // --- Vault ---------------------------------------------------------------------------------

    public async Task<VaultedCardResult> VaultCardAsync(VaultCardGatewayRequest request, CancellationToken cancellationToken = default)
    {
        using var cts = Budget(cancellationToken, CallBudget);

        // Associate the card with the shopper's existing PayPal customer when they have one; for their
        // first card we omit the customer entirely and PayPal generates (and returns) the customer id.
        // We deliberately do NOT send merchant_customer_id — the sandbox returns 500 when it is present.
        var customer = string.IsNullOrEmpty(request.ExistingPayPalCustomerId)
            ? null
            : new Customer { Id = request.ExistingPayPalCustomerId };

        var body = new PaymentTokenRequest
        {
            Customer = customer,
            PaymentSource = new PaymentTokenRequestPaymentSource
            {
                Card = new PaymentTokenRequestCard
                {
                    Number = request.Card.Number,
                    Expiry = request.Card.Expiry,
                    SecurityCode = request.Card.SecurityCode,
                    Name = request.Card.CardholderName,
                    BillingAddress = BuildAddress(request.Card.BillingAddress)
                }
            }
        };

        PaymentTokenResponse response;
        try
        {
            response = await _client.Vault.CreatePaymentToken(new CreatePaymentTokenRequest
            {
                Body = body,
                PayPalRequestId = request.IdempotencyKey
            }, cancellationToken: cts.Token);
        }
        catch (ApiException<CreatePaymentTokenError> ex)
        {
            throw TranslateTyped(ex, "vault card", ex.Error.TryGetError);
        }
        catch (Exception ex) when (TryTranslateInfra(ex, isWrite: true, "vault card", out var translated))
        {
            throw translated;
        }

        EnsureVaultNotChallenged(response.Links, "card vault");

        var vaultId = response.Id ?? throw new PaymentGatewayException("PayPal did not return a vault id for the card.", null, false);
        var customerId = response.Customer?.Id ?? request.ExistingPayPalCustomerId
            ?? throw new PaymentGatewayException("PayPal did not return a customer id for the vaulted card.", null, false);
        var card = response.PaymentSource?.Card;

        return new VaultedCardResult(vaultId, customerId, card?.Brand?.Value, card?.LastDigits, card?.Expiry, card?.Name);
    }

    private static void EnsureVaultNotChallenged(IReadOnlyList<LinkDescription>? links, string stage)
    {
        var needsApproval = links?.Any(l =>
            string.Equals(l.Rel, "approve", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.Rel, "payer-action", StringComparison.OrdinalIgnoreCase)) ?? false;

        if (needsApproval)
        {
            throw new PaymentChallengeRequiredException(
                $"PayPal requires the shopper to approve this card in a browser ({stage}). This vault flow is not supported without an approval step.");
        }
    }

    public async Task DeleteVaultedCardAsync(string vaultId, CancellationToken cancellationToken = default)
    {
        using var cts = Budget(cancellationToken, CallBudget);
        try
        {
            await _client.Vault.DeletePaymentToken(new DeletePaymentTokenRequest { Id = vaultId }, cancellationToken: cts.Token);
        }
        catch (ApiException<DeletePaymentTokenError> ex)
        {
            throw TranslateTyped(ex, "delete vault card", ex.Error.TryGetError);
        }
        catch (Exception ex) when (TryTranslateInfra(ex, isWrite: true, "delete vault card", out var translated))
        {
            throw translated;
        }
    }

    // --- Transaction search --------------------------------------------------------------------

    public async Task<TransactionSearchResult> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        using var cts = Budget(cancellationToken, SearchBudget);
        var token = cts.Token;

        var results = new List<ReconciliationTransaction>();
        var seen = new HashSet<string>();
        var complete = true;

        // PayPal caps a single query at 31 days, so walk the range in sub-windows, every page of each.
        var cursor = from;
        while (cursor < to)
        {
            var windowEnd = cursor.AddDays(31);
            if (windowEnd >= to)
            {
                windowEnd = to;
            }

            var page = 1;
            var totalPages = 1;
            do
            {
                SearchResponse response;
                try
                {
                    response = await _client.TransactionSearch.SearchTransactions(new SearchTransactionsRequest
                    {
                        StartDate = FormatRfc3339(cursor),
                        EndDate = FormatRfc3339(windowEnd),
                        Fields = "transaction_info",
                        BalanceAffectingRecordsOnly = "N",
                        PageSize = 500,
                        Page = page
                    }, cancellationToken: token);
                }
                catch (ApiException<RawError> ex)
                {
                    throw FromStatus((int)ex.Error.StatusCode, "transaction search");
                }
                catch (Exception ex) when (TryTranslateInfra(ex, isWrite: false, "transaction search", out var translated))
                {
                    throw translated;
                }

                foreach (var detail in response.TransactionDetails ?? Enumerable.Empty<TransactionDetails>())
                {
                    var info = detail.TransactionInfo;
                    if (info?.TransactionId is null || !seen.Add(info.TransactionId + "|" + info.TransactionEventCode))
                    {
                        continue;
                    }

                    results.Add(new ReconciliationTransaction(
                        info.TransactionId,
                        info.TransactionStatus,
                        ParseMoney(info.TransactionAmount),
                        info.TransactionAmount?.CurrencyCode,
                        ParseMoney(info.FeeAmount),
                        info.InvoiceId,
                        info.CustomField,
                        info.TransactionEventCode,
                        ParseDate(info.TransactionInitiationDate)));
                }

                totalPages = response.TotalPages ?? 1;
                page++;

                if (page > MaxPagesPerWindow)
                {
                    complete = false;
                    break;
                }
            }
            while (page <= totalPages && complete);

            if (windowEnd >= to)
            {
                break;
            }
            cursor = windowEnd.AddSeconds(1);
        }

        return new TransactionSearchResult(results, complete);
    }

    // --- helpers -------------------------------------------------------------------------------

    private static CancellationTokenSource Budget(CancellationToken ct, TimeSpan budget)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(budget);
        return cts;
    }

    private static CardRequest BuildCard(AuthorizeGatewayRequest request)
    {
        if (!string.IsNullOrEmpty(request.SavedCardVaultId))
        {
            return new CardRequest { VaultId = request.SavedCardVaultId };
        }

        var card = request.Card!;
        return new CardRequest
        {
            Number = card.Number,
            Expiry = card.Expiry,
            SecurityCode = card.SecurityCode,
            Name = card.CardholderName,
            BillingAddress = BuildAddress(card.BillingAddress)
        };
    }

    private static Address? BuildAddress(BillingAddressInput? input)
    {
        if (input is null)
        {
            return null;
        }
        return new Address
        {
            AddressLine1 = input.AddressLine1,
            AddressLine2 = input.AddressLine2,
            AdminArea2 = input.City,
            AdminArea1 = input.State,
            PostalCode = input.PostalCode,
            CountryCode = input.CountryCode ?? "US"
        };
    }

    private static AuthorizationWithAdditionalData? FindAuthorization(IReadOnlyList<PurchaseUnit>? purchaseUnits) =>
        purchaseUnits?
            .SelectMany(pu => pu.Payments?.Authorizations ?? Enumerable.Empty<AuthorizationWithAdditionalData>())
            .FirstOrDefault();

    /// <summary>
    /// Detects a response that needs a shopper to approve in a browser and stops, rather than building
    /// an approval round-trip.
    /// </summary>
    private static void EnsureNoChallenge(OrderStatus? status, IReadOnlyList<LinkDescription>? links, AuthorizationWithAdditionalData? authorization, string stage)
    {
        var needsAction = status == OrderStatus.PayerActionRequired ||
            (links?.Any(l => string.Equals(l.Rel, "payer-action", StringComparison.OrdinalIgnoreCase)) ?? false);

        if (needsAction && authorization is null)
        {
            throw new PaymentChallengeRequiredException(
                $"PayPal requires the shopper to approve this payment in a browser ({stage}). This card flow is not supported without an approval step.");
        }
    }

    private delegate bool TryGetTypedError(out Error error);

    private PaymentGatewayException TranslateTyped<TError>(ApiException<TError> ex, string operation, TryGetTypedError tryGetError)
    {
        var status = (int)ex.StatusCode;
        if (tryGetError(out var error))
        {
            return FromError(status, error, operation);
        }
        return FromStatus(status, operation);
    }

    private PaymentGatewayException FromError(int status, Error error, string operation)
    {
        var issue = error.Details?.FirstOrDefault()?.Issue ?? error.Name;
        var message = BuildCallerMessage(error.Message, issue, operation);
        _logger.LogWarning($"PayPal {operation} failed: HTTP {status} name={error.Name} issue={issue} debug_id={error.DebugId}");
        return new PaymentGatewayException(message, status, IsCallerError(status), issue, error.DebugId);
    }

    private PaymentGatewayException FromStatus(int status, string operation)
    {
        _logger.LogWarning($"PayPal {operation} failed: HTTP {status} (no typed error body).");
        return new PaymentGatewayException($"PayPal rejected the {operation} request (HTTP {status}).", status, IsCallerError(status));
    }

    private static string BuildCallerMessage(string? message, string? issue, string operation)
    {
        if (!string.IsNullOrWhiteSpace(message) && !string.IsNullOrWhiteSpace(issue))
        {
            return $"{message} ({issue})";
        }
        return message ?? issue ?? $"PayPal rejected the {operation} request.";
    }

    /// <summary>A 4xx other than auth/throttling is the caller's to fix; everything else is ours or transport.</summary>
    private static bool IsCallerError(int status) => status is >= 400 and < 500 && status is not (401 or 403 or 429);

    private bool TryTranslateInfra(Exception ex, bool isWrite, string operation, out PaymentGatewayException translated)
    {
        switch (ex)
        {
            case ResponseDeserializationException d:
                var status = (int)d.StatusCode;
                _logger.LogWarning($"PayPal {operation}: undeserializable response (HTTP {status}).");
                translated = new PaymentGatewayException(
                    "PayPal returned a response that could not be processed.", status, IsCallerError(status), inner: d)
                {
                    OutcomeUnknown = isWrite && status is >= 200 and < 300
                };
                return true;

            case SdkTimeoutException t:
                _logger.LogWarning($"PayPal {operation}: timed out after {t.Timeout}.");
                translated = new PaymentGatewayException("PayPal did not answer in time.", null, isCallerError: false, inner: t)
                {
                    OutcomeUnknown = isWrite
                };
                return true;

            case SdkConnectionException c:
                _logger.LogWarning($"PayPal {operation}: connection failure ({c.Message}).");
                translated = new PaymentGatewayException("PayPal could not be reached.", null, isCallerError: false, inner: c)
                {
                    OutcomeUnknown = isWrite
                };
                return true;

            case AuthSchemeException a:
                _logger.LogWarning($"PayPal {operation}: credentials could not be applied: {a.Message}");
                translated = new PaymentGatewayException("PayPal credentials were rejected.", null, isCallerError: false, inner: a);
                return true;

            default:
                translated = null!;
                return false;
        }
    }

    private static string FormatAmount(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatRfc3339(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static decimal? ParseMoney(Money? money)
    {
        if (money?.Value is null)
        {
            return null;
        }
        return decimal.TryParse(money.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;
    }
}
