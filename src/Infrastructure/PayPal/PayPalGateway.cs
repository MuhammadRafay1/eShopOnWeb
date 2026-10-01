using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;
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
using SdkError = PayPalServerSdk.Models.Error;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// The sole place any PayPal Server SDK call is made. Wraps each capability, carries the idempotency
/// keys, and translates SDK exceptions into the application's own <see cref="PaymentGatewayException"/>
/// family so no SDK type crosses out of Infrastructure.
/// </summary>
public sealed class PayPalGateway : IPayPalGateway
{
    private const string Representation = "return=representation";

    // Whole-call budget (the only thing that bounds a whole call; the SDK's Timeout is per attempt).
    private static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(30);

    private readonly PayPalServerSdkClient _client;
    private readonly ILogger<PayPalGateway> _logger;

    public PayPalGateway(PayPalServerSdkClient client, Microsoft.Extensions.Options.IOptions<PayPalOptions> options, ILogger<PayPalGateway> logger)
    {
        _client = client;
        _logger = logger;
        Currency = options.Value.Currency;
    }

    public string Currency { get; }

    public Task<PayPalAuthorizationResult> AuthorizeAsync(AuthorizeCardPaymentCommand command, CancellationToken cancellationToken)
    {
        PaymentSource paymentSource = command.VaultId is not null
            ? new PaymentSource { Card = new CardRequest { VaultId = command.VaultId } }
            : new PaymentSource { Card = BuildCard(command.Card!) };

        var body = new OrderRequest
        {
            Intent = CheckoutPaymentIntent.Authorize,
            PurchaseUnits = new List<PurchaseUnitRequest>
            {
                new()
                {
                    Amount = new AmountWithBreakdown { CurrencyCode = Currency, Value = Money2(command.Amount) },
                    InvoiceId = command.InvoiceId,
                    CustomId = command.InvoiceId,
                    Description = Truncate(command.Description, 127)
                }
            },
            PaymentSource = paymentSource
        };

        var request = new CreateOrderRequest { Body = body, PayPalRequestId = command.IdempotencyKey, Prefer = Representation };

        _logger.LogInformation("PayPal CreateOrder: invoice {Invoice}, amount {Amount} {Currency}, source {Source}.",
            command.InvoiceId, Money2(command.Amount), Currency, command.VaultId is null ? "card" : "vault");

        return ExecuteAsync<PayPalAuthorizationResult, Order, CreateOrderError>(
            "CreateOrder", isWrite: true, cancellationToken,
            ct => _client.Orders.CreateOrder(request, cancellationToken: ct),
            e => e.TryGetError(out var er) ? er : null,
            order =>
            {
                if (order.Status == OrderStatus.PayerActionRequired)
                    throw new PayPalPayerActionRequiredException(
                        "PayPal requires payer action (3DS / browser approval) to complete this card payment. " +
                        "Per the integration contract this is a stop condition; no approval round-trip is performed.");

                var auth = order.PurchaseUnits?.FirstOrDefault()?.Payments?.Authorizations?.FirstOrDefault();
                if (auth?.Id is null)
                    throw new PaymentGatewayException(
                        $"PayPal returned no authorization for the order (status {order.Status?.Value ?? "unknown"}).");

                if (auth.Status == AuthorizationStatus.Denied)
                    throw new PaymentGatewayException("The card authorization was denied by PayPal.");

                return new PayPalAuthorizationResult
                {
                    PayPalOrderId = order.Id ?? string.Empty,
                    OrderStatus = order.Status?.Value ?? string.Empty,
                    AuthorizationId = auth.Id,
                    AuthorizationStatus = auth.Status?.Value,
                    AuthorizedAmount = ParseAmount(auth.Amount),
                    ExpiresAt = ParseDate(auth.ExpirationTime)
                };
            });
    }

    public Task<PayPalAuthorizationState> GetAuthorizationAsync(string authorizationId, CancellationToken cancellationToken)
    {
        var request = new GetAuthorizedPaymentRequest { AuthorizationId = authorizationId };
        return ExecuteAsync<PayPalAuthorizationState, PaymentAuthorization, GetAuthorizedPaymentError>(
            "GetAuthorizedPayment", isWrite: false, cancellationToken,
            ct => _client.Payments.GetAuthorizedPayment(request, cancellationToken: ct),
            e => e.TryGetError(out var er) ? er : null,
            pa => new PayPalAuthorizationState
            {
                AuthorizationId = pa.Id ?? authorizationId,
                Status = pa.Status?.Value,
                ExpiresAt = ParseDate(pa.ExpirationTime)
            });
    }

    public Task<PayPalAuthorizationState> ReauthorizeAsync(string authorizationId, decimal amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        var request = new ReauthorizePaymentRequest
        {
            AuthorizationId = authorizationId,
            Body = new ReauthorizeRequest { Amount = new Money { CurrencyCode = Currency, Value = Money2(amount) } },
            PayPalRequestId = idempotencyKey,
            Prefer = Representation
        };
        return ExecuteAsync<PayPalAuthorizationState, PaymentAuthorization, ReauthorizePaymentError>(
            "ReauthorizePayment", isWrite: true, cancellationToken,
            ct => _client.Payments.ReauthorizePayment(request, cancellationToken: ct),
            e => e.TryGetError(out var er) ? er : null,
            pa => new PayPalAuthorizationState
            {
                // Defensive: take the id to capture from the response's own id, never assume it is unchanged.
                AuthorizationId = pa.Id ?? authorizationId,
                Status = pa.Status?.Value,
                ExpiresAt = ParseDate(pa.ExpirationTime)
            });
    }

    public Task<PayPalCaptureResult> CaptureAsync(string authorizationId, decimal amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        var request = new CaptureAuthorizedPaymentRequest
        {
            AuthorizationId = authorizationId,
            // Capture the exact held amount explicitly (final capture — no further captures).
            Body = new CaptureRequest { Amount = new Money { CurrencyCode = Currency, Value = Money2(amount) }, FinalCapture = true },
            PayPalRequestId = idempotencyKey,
            Prefer = Representation
        };
        return ExecuteAsync<PayPalCaptureResult, CapturedPayment, CaptureAuthorizedPaymentError>(
            "CaptureAuthorizedPayment", isWrite: true, cancellationToken,
            ct => _client.Payments.CaptureAuthorizedPayment(request, cancellationToken: ct),
            e => e.TryGetError(out var er) ? er : null,
            cap =>
            {
                if (cap.Id is null)
                    throw new PaymentGatewayException("PayPal returned no capture id for the fulfilment.");
                var breakdown = cap.SellerReceivableBreakdown;
                return new PayPalCaptureResult
                {
                    CaptureId = cap.Id,
                    Status = cap.Status?.Value,
                    GrossAmount = ParseAmount(breakdown?.GrossAmount) ?? ParseAmount(cap.Amount) ?? 0m,
                    PayPalFee = ParseAmount(breakdown?.PaypalFee),
                    NetAmount = ParseAmount(breakdown?.NetAmount),
                    Currency = breakdown?.GrossAmount?.CurrencyCode ?? cap.Amount?.CurrencyCode ?? string.Empty
                };
            });
    }

    public Task<PayPalVoidResult> VoidAsync(string authorizationId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var request = new VoidPaymentRequest
        {
            AuthorizationId = authorizationId,
            PayPalRequestId = idempotencyKey,
            Prefer = Representation
        };
        return ExecuteAsync<PayPalVoidResult, PaymentAuthorization, VoidPaymentError>(
            "VoidPayment", isWrite: true, cancellationToken,
            ct => _client.Payments.VoidPayment(request, cancellationToken: ct),
            e => e.TryGetError(out var er) ? er : null,
            pa => new PayPalVoidResult { Status = pa?.Status?.Value ?? "VOIDED" });
    }

    public Task<PayPalRefundResult> RefundAsync(string captureId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        var body = amount is null
            ? new RefundRequest()
            : new RefundRequest { Amount = new Money { CurrencyCode = Currency, Value = Money2(amount.Value) } };

        var request = new RefundCapturedPaymentRequest
        {
            CaptureId = captureId,
            Body = body,
            PayPalRequestId = idempotencyKey,
            Prefer = Representation
        };
        return ExecuteAsync<PayPalRefundResult, Refund, RefundCapturedPaymentError>(
            "RefundCapturedPayment", isWrite: true, cancellationToken,
            ct => _client.Payments.RefundCapturedPayment(request, cancellationToken: ct),
            e => e.TryGetError(out var er) ? er : null,
            r =>
            {
                if (r.Id is null)
                    throw new PaymentGatewayException("PayPal returned no refund id.");
                return new PayPalRefundResult { RefundId = r.Id, Status = r.Status?.Value, Amount = ParseAmount(r.Amount) };
            });
    }

    public async Task<PayPalSavedCardResult> SaveCardAsync(SaveCardCommand command, CancellationToken cancellationToken)
    {
        // 1) Create a setup token that captures the card. Only an existing shopper's customer id is sent
        // (to keep all their cards under one PayPal customer); for a new shopper PayPal mints the id, which
        // we read from the payment token below.
        var customer = command.ExistingPayPalCustomerId is not null
            ? new Customer { Id = command.ExistingPayPalCustomerId }
            : null;

        var setupBody = new SetupTokenRequest
        {
            Customer = customer,
            PaymentSource = new SetupTokenRequestPaymentSource
            {
                Card = new SetupTokenRequestCard
                {
                    Number = command.Card.Number,
                    Expiry = command.Card.Expiry,
                    Name = command.Card.Name,
                    BillingAddress = BuildAddress(command.Card.BillingAddress)
                }
            }
        };
        var setupRequest = new CreateSetupTokenRequest { Body = setupBody, PayPalRequestId = Guid.NewGuid().ToString() };

        var setupToken = await ExecuteAsync<SetupTokenResponse, SetupTokenResponse, CreateSetupTokenError>(
            "CreateSetupToken", isWrite: true, cancellationToken,
            ct => _client.Vault.CreateSetupToken(setupRequest, cancellationToken: ct),
            e => e.TryGetError(out var er) ? er : null,
            st =>
            {
                if (st.Status == PaymentTokenStatus.PayerActionRequired)
                    throw new PayPalPayerActionRequiredException(
                        "PayPal requires payer action (3DS / browser approval) to vault this card. " +
                        "Per the integration contract this is a stop condition; no approval round-trip is performed.");
                if (st.Id is null)
                    throw new PaymentGatewayException("PayPal returned no setup token id.");
                return st;
            });

        var customerId = setupToken.Customer?.Id ?? command.ExistingPayPalCustomerId;

        // 2) Exchange the setup token for a permanent payment (vault) token.
        var tokenCustomer = customerId is not null ? new Customer { Id = customerId } : null;
        var tokenBody = new PaymentTokenRequest
        {
            Customer = tokenCustomer,
            PaymentSource = new PaymentTokenRequestPaymentSource
            {
                Token = new VaultTokenRequest { Id = setupToken.Id!, Type = VaultTokenRequestType.SetupToken }
            }
        };
        var tokenRequest = new CreatePaymentTokenRequest { Body = tokenBody, PayPalRequestId = Guid.NewGuid().ToString() };

        return await ExecuteAsync<PayPalSavedCardResult, PaymentTokenResponse, CreatePaymentTokenError>(
            "CreatePaymentToken", isWrite: true, cancellationToken,
            ct => _client.Vault.CreatePaymentToken(tokenRequest, cancellationToken: ct),
            e => e.TryGetError(out var er) ? er : null,
            pt =>
            {
                if (pt.Id is null)
                    throw new PaymentGatewayException("PayPal returned no vault (payment token) id.");
                var card = pt.PaymentSource?.Card;
                var resolvedCustomer = pt.Customer?.Id ?? customerId;
                if (resolvedCustomer is null)
                    throw new PaymentGatewayException("PayPal returned no customer id for the vaulted card.");
                return new PayPalSavedCardResult
                {
                    VaultId = pt.Id,
                    PayPalCustomerId = resolvedCustomer,
                    Brand = card?.Brand?.Value,
                    LastDigits = card?.LastDigits,
                    Expiry = card?.Expiry
                };
            });
    }

    public Task DeleteSavedCardAsync(string vaultId, CancellationToken cancellationToken)
    {
        var request = new DeletePaymentTokenRequest { Id = vaultId };
        return ExecuteAsync<bool, bool, DeletePaymentTokenError>(
            "DeletePaymentToken", isWrite: true, cancellationToken,
            async ct =>
            {
                await _client.Vault.DeletePaymentToken(request, cancellationToken: ct);
                return true;
            },
            e => e.TryGetError(out var er) ? er : null,
            _ => true);
    }

    public async Task<PayPalTransactionReport> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        const int PageSize = 500;   // the maximum the request record allows
        const int MaxPages = 200;   // hard backstop so the loop can never run unbounded

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(CallBudget);
        var ct = cts.Token;

        var transactions = new List<PayPalTransaction>();
        var truncated = false;
        var page = 1;

        while (true)
        {
            var request = new SearchTransactionsRequest
            {
                StartDate = Rfc3339(from),
                EndDate = Rfc3339(to),
                Fields = "transaction_info",
                BalanceAffectingRecordsOnly = "Y",
                PageSize = PageSize,
                Page = page
            };

            SearchResponse response;
            try
            {
                response = await _client.TransactionSearch.SearchTransactions(request, cancellationToken: ct);
            }
            catch (ApiException<RawError> ex)
            {
                _logger.LogWarning("PayPal SearchTransactions failed: HTTP {Status}", (int)ex.StatusCode);
                throw new PaymentGatewayException($"PayPal rejected the reconciliation search (HTTP {(int)ex.StatusCode}).", ex.StatusCode, inner: ex);
            }
            catch (ResponseDeserializationException ex)
            {
                throw new PaymentGatewayException("PayPal returned a reconciliation response that could not be processed.", ex.StatusCode, inner: ex);
            }
            catch (SdkException ex) when (ex is SdkConnectionException or SdkTimeoutException)
            {
                throw new PaymentGatewayException("PayPal was unreachable during the reconciliation search.", inner: ex);
            }

            foreach (var detail in response.TransactionDetails ?? Enumerable.Empty<TransactionDetails>())
            {
                var info = detail.TransactionInfo;
                if (info is null) continue;
                transactions.Add(new PayPalTransaction
                {
                    TransactionId = info.TransactionId,
                    ReferenceId = info.PaypalReferenceId,
                    Amount = ParseAmount(info.TransactionAmount),
                    Currency = info.TransactionAmount?.CurrencyCode,
                    Status = info.TransactionStatus,
                    InvoiceId = info.InvoiceId,
                    InitiatedAt = ParseDate(info.TransactionInitiationDate)
                });
            }

            var totalPages = response.TotalPages ?? page;
            if (page >= totalPages)
                break;

            if (page >= MaxPages)
            {
                truncated = true;
                break;
            }

            page++;
        }

        return new PayPalTransactionReport { Transactions = transactions, Truncated = truncated };
    }

    // --- Execution / translation ---

    private async Task<TResult> ExecuteAsync<TResult, TBody, TError>(
        string operation,
        bool isWrite,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<TBody>> call,
        Func<TError, SdkError?> extractError,
        Func<TBody, TResult> map)
        where TError : ApiError
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(CallBudget);
        var ct = cts.Token;

        try
        {
            return map(await call(ct));
        }
        catch (ApiException<TError> ex)
        {
            throw TranslateTyped(operation, ex, extractError);
        }
        catch (ResponseDeserializationException ex)
        {
            _logger.LogWarning(ex, "PayPal {Operation} returned an unprocessable response (HTTP {Status}).", operation, (int?)ex.StatusCode);
            throw new PaymentGatewayException($"PayPal returned a response for {operation} that could not be processed.", ex.StatusCode, inner: ex);
        }
        catch (AuthSchemeException ex)
        {
            _logger.LogError(ex, "PayPal credentials were rejected while calling {Operation}.", operation);
            throw new PaymentGatewayException($"PayPal credentials were rejected while calling {operation}.", inner: ex);
        }
        catch (SdkException ex) when (ex is SdkConnectionException or SdkTimeoutException)
        {
            if (isWrite)
            {
                // Resend once with the SAME idempotency key — PayPal dedupes, so this cannot double-charge.
                try
                {
                    return map(await call(ct));
                }
                catch (SdkException)
                {
                    // still unsettled
                }
                _logger.LogError(ex, "PayPal {Operation} connection failed; outcome unknown.", operation);
                throw new PaymentOutcomeUnknownException(
                    $"The connection to PayPal failed during {operation} and the outcome could not be confirmed.", ex);
            }
            _logger.LogWarning(ex, "PayPal {Operation} was unreachable.", operation);
            throw new PaymentGatewayException($"PayPal was unreachable while calling {operation}.", inner: ex);
        }
    }

    private PaymentGatewayException TranslateTyped<TError>(string operation, ApiException<TError> ex, Func<TError, SdkError?> extractError)
        where TError : ApiError
    {
        string? debugId = null;
        string? name = null;

        var typed = extractError(ex.Error);
        if (typed is not null)
        {
            debugId = typed.DebugId;
            name = typed.Name;
            if (typed.Details is { Count: > 0 } details)
            {
                var issues = string.Join("; ", details.Select(d => $"{d.Issue}({d.Field ?? "-"})"));
                _logger.LogWarning("PayPal {Operation} issue detail: {Issues}", operation, issues);
            }
        }
        else if (ex.Error.TryGetRawError(out var raw))
        {
            try
            {
                var body = raw.ReadAsJson<SdkError>();
                debugId = body?.DebugId;
                name = body?.Name;
            }
            catch
            {
                // body was not the standard error shape; status alone is what we carry.
            }
        }

        _logger.LogWarning("PayPal {Operation} failed: HTTP {Status}, name {Name}, debugId {DebugId}.",
            operation, (int?)ex.StatusCode, name ?? "(none)", debugId ?? "(none)");

        var detail = name is null ? string.Empty : $", {name}";
        var correlation = debugId is null ? string.Empty : $", PayPal debug id {debugId}";
        return new PaymentGatewayException(
            $"PayPal rejected {operation} (HTTP {(int?)ex.StatusCode}{detail}{correlation}).",
            ex.StatusCode, debugId, ex);
    }

    // --- Mapping helpers ---

    private static CardRequest BuildCard(CardDetails card) => new()
    {
        Number = card.Number,
        Expiry = card.Expiry,
        SecurityCode = card.SecurityCode,
        Name = card.Name,
        BillingAddress = BuildAddress(card.BillingAddress)
    };

    private static Address BuildAddress(CardBillingAddress billing) => new()
    {
        AddressLine1 = billing.Line1,
        AddressLine2 = billing.Line2,
        AdminArea2 = billing.City,
        AdminArea1 = billing.State,
        PostalCode = billing.PostalCode,
        CountryCode = billing.CountryCode
    };

    private static string Money2(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

    private static decimal? ParseAmount(Money? money) =>
        money?.Value is { } v && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static DateTimeOffset? ParseDate(string? value) =>
        value is { Length: > 0 } && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt : null;

    private static string Rfc3339(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
