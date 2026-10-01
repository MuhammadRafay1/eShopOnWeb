using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.ApplicationCore.Services.Payments;
using Microsoft.Extensions.Logging;
using PayPalServerSdk;
using PayPalServerSdk.Core.ErrorResponse;
using PayPalServerSdk.Core.Exceptions;
using PayPalServerSdk.Errors;
using PayPalServerSdk.Models;
using PayPalServerSdk.Models.Enums;
using SdkPaymentSource = PayPalServerSdk.Models.PaymentSource;
using SdkAddress = PayPalServerSdk.Models.Address;
using DomainPaymentSource = Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments.PaymentSource;
using RequestsOrders = PayPalServerSdk.Requests.Orders;
using RequestsPayments = PayPalServerSdk.Requests.Payments;
using RequestsVault = PayPalServerSdk.Requests.Vault;
using RequestsTxnSearch = PayPalServerSdk.Requests.TransactionSearch;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// The sole caller of the PayPal SDK in this application. Every ApplicationCore type stays
/// SDK-free; this class is the one seam that translates between the two.
/// </summary>
public class PayPalPaymentGateway : IPayPalPaymentGateway
{
    private readonly PayPalServerSdkClient _client;
    private readonly ILogger<PayPalPaymentGateway> _logger;

    public PayPalPaymentGateway(PayPalServerSdkClient client, ILogger<PayPalPaymentGateway> logger)
    {
        _client = client;
        _logger = logger;
    }

    public Task<AuthorizeResult> AuthorizeOrderAsync(AuthorizeOrderCommand command, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var amountValue = CurrencyFormat.Format(command.Amount, command.CurrencyCode);

            Order created;
            try
            {
                created = await _client.Orders.CreateOrder(new RequestsOrders.CreateOrderRequest
                {
                    PayPalRequestId = $"ord-{command.OrderId}",
                    Prefer = "return=representation",
                    Body = new OrderRequest
                    {
                        Intent = CheckoutPaymentIntent.Authorize,
                        PurchaseUnits = new List<PurchaseUnitRequest>
                        {
                            new()
                            {
                                Amount = new AmountWithBreakdown { CurrencyCode = command.CurrencyCode, Value = amountValue },
                                CustomId = command.OrderId.ToString(CultureInfo.InvariantCulture),
                                InvoiceId = command.InvoiceId
                            }
                        },
                        PaymentSource = BuildPaymentSource(command.PaymentSource)
                    }
                }, cancellationToken: cancellationToken);
            }
            catch (ApiException<CreateOrderError> ex)
            {
                throw MapCaseA(ex.Error, (int)ex.StatusCode);
            }

            EnsureNoChallenge(created.Status);

            // When payment_source.card is supplied at create time, PayPal processes the authorization
            // synchronously as part of CreateOrder itself - the authorization is already on the created
            // order. Only fall back to the explicit AuthorizeOrder call when it is not (e.g. a buyer-approval
            // flow with no card on the create request). Calling AuthorizeOrder on an order PayPal already
            // finished processing is itself refused by PayPal, which is what an unconditional second call was hitting.
            var authorization = created.PurchaseUnits?.FirstOrDefault()?.Payments?.Authorizations?.FirstOrDefault();

            if (authorization?.Id is null)
            {
                OrderAuthorizeResponse authorized;
                try
                {
                    authorized = await _client.Orders.AuthorizeOrder(new RequestsOrders.AuthorizeOrderRequest
                    {
                        Id = created.Id,
                        PayPalRequestId = $"auth-{command.OrderId}",
                        Prefer = "return=representation"
                    }, cancellationToken: cancellationToken);
                }
                catch (ApiException<AuthorizeOrderError> ex)
                {
                    throw MapCaseA(ex.Error, (int)ex.StatusCode);
                }

                EnsureNoChallenge(authorized.Status);
                authorization = authorized.PurchaseUnits?.FirstOrDefault()?.Payments?.Authorizations?.FirstOrDefault();
            }

            if (authorization?.Id is null)
            {
                throw new PayPalGatewayException("PayPal did not return an authorization for this order.", (int)HttpStatusCode.OK, null);
            }

            var descriptor = DescribePaymentSource(command.PaymentSource);

            _logger.LogInformation(
                "PayPal authorize: order={OrderId} payPalOrderId={PayPalOrderId} authorizationId={AuthorizationId} status={Status} held={HeldAmount} {HeldCurrency}",
                command.OrderId, created.Id, authorization.Id, authorization.Status?.Value, authorization.Amount?.Value, authorization.Amount?.CurrencyCode);

            return new AuthorizeResult(
                created.Id!,
                authorization.Id!,
                authorization.Status?.Value ?? "UNKNOWN",
                ToExpiryString(authorization.ExpirationTime),
                descriptor);
        });

    public Task<CaptureResult> CaptureAsync(CaptureCommand command, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            CapturedPayment captured;
            try
            {
                captured = await _client.Payments.CaptureAuthorizedPayment(new RequestsPayments.CaptureAuthorizedPaymentRequest
                {
                    AuthorizationId = command.AuthorizationId,
                    PayPalRequestId = $"cap-{command.OrderId}",
                    Prefer = "return=representation",
                    Body = new CaptureRequest { FinalCapture = true }
                }, cancellationToken: cancellationToken);
            }
            catch (ApiException<CaptureAuthorizedPaymentError> ex)
            {
                if (ex.Error.TryGetError(out var err))
                {
                    if (IsAuthorizationStale(err))
                    {
                        throw new AuthorizationStaleException();
                    }

                    throw MapError(err, (int)ex.StatusCode);
                }

                if (ex.Error.TryGetNoContent(out var noContent))
                {
                    throw MapRaw(noContent);
                }

                throw MapFallback(ex.Error, (int)ex.StatusCode);
            }

            var gross = captured.SellerReceivableBreakdown?.GrossAmount?.Value;
            var fee = captured.SellerReceivableBreakdown?.PaypalFee?.Value;
            var net = captured.SellerReceivableBreakdown?.NetAmount?.Value;

            _logger.LogInformation(
                "PayPal capture: order={OrderId} authorizationId={AuthorizationId} captureId={CaptureId} status={Status} gross={Gross} fee={Fee} net={Net}",
                command.OrderId, command.AuthorizationId, captured.Id, captured.Status?.Value, gross, fee, net);

            return new CaptureResult(
                captured.Id!,
                captured.Status?.Value ?? "UNKNOWN",
                gross is not null ? CurrencyFormat.Parse(gross) : 0m,
                fee is not null ? CurrencyFormat.Parse(fee) : null,
                net is not null ? CurrencyFormat.Parse(net) : null);
        });

    public Task<ReauthorizeResult> ReauthorizeAsync(ReauthorizeCommand command, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            PaymentAuthorization reauthorized;
            try
            {
                reauthorized = await _client.Payments.ReauthorizePayment(new RequestsPayments.ReauthorizePaymentRequest
                {
                    AuthorizationId = command.AuthorizationId,
                    PayPalRequestId = $"reauth-{command.AuthorizationId}",
                    Prefer = "return=representation",
                    Body = new ReauthorizeRequest
                    {
                        Amount = new Money { CurrencyCode = command.CurrencyCode, Value = CurrencyFormat.Format(command.Amount, command.CurrencyCode) }
                    }
                }, cancellationToken: cancellationToken);
            }
            catch (ApiException<ReauthorizePaymentError> ex)
            {
                if (ex.Error.TryGetError(out var err)) throw MapError(err, (int)ex.StatusCode);
                if (ex.Error.TryGetNoContent(out var noContent)) throw MapRaw(noContent);
                throw MapFallback(ex.Error, (int)ex.StatusCode);
            }

            return new ReauthorizeResult(reauthorized.Id!, reauthorized.Status?.Value ?? "UNKNOWN", ToExpiryString(reauthorized.ExpirationTime));
        });

    public Task VoidAsync(string authorizationId, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            try
            {
                await _client.Payments.VoidPayment(new RequestsPayments.VoidPaymentRequest
                {
                    AuthorizationId = authorizationId,
                    PayPalRequestId = $"void-{authorizationId}",
                    Prefer = "return=representation"
                }, cancellationToken: cancellationToken);
            }
            catch (ApiException<VoidPaymentError> ex)
            {
                if (ex.Error.TryGetError(out var err)) throw MapError(err, (int)ex.StatusCode);
                if (ex.Error.TryGetNoContent(out var noContent)) throw MapRaw(noContent);
                throw MapFallback(ex.Error, (int)ex.StatusCode);
            }

            return true;
        });

    public Task<RefundResult> RefundAsync(RefundCommand command, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            Refund refund;
            try
            {
                refund = await _client.Payments.RefundCapturedPayment(new RequestsPayments.RefundCapturedPaymentRequest
                {
                    CaptureId = command.CaptureId,
                    PayPalRequestId = command.IdempotencyKey,
                    Prefer = "return=representation",
                    Body = new RefundRequest
                    {
                        Amount = command.Amount is null
                            ? null
                            : new Money { CurrencyCode = command.CurrencyCode, Value = CurrencyFormat.Format(command.Amount.Value, command.CurrencyCode) }
                    }
                }, cancellationToken: cancellationToken);
            }
            catch (ApiException<RefundCapturedPaymentError> ex)
            {
                if (ex.Error.TryGetError(out var err)) throw MapError(err, (int)ex.StatusCode);
                if (ex.Error.TryGetNoContent(out var noContent)) throw MapRaw(noContent);
                throw MapFallback(ex.Error, (int)ex.StatusCode);
            }

            var amount = refund.Amount?.Value is not null ? CurrencyFormat.Parse(refund.Amount.Value) : (command.Amount ?? 0m);
            return new RefundResult(refund.Id!, refund.Status?.Value ?? "UNKNOWN", amount);
        });

    public Task<VaultCardResult> SaveCardAsync(SaveCardCommand command, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            // Direct one-step card vaulting (payment_source.card on CreatePaymentToken) was observed to be
            // rejected by this sandbox account with a bare 500 INTERNAL_SERVER_ERROR (no detail). Fall back
            // to the SDK's two-step setup-token flow, which this account does support: create a setup token
            // from the card, then exchange it for a payment token.
            var customer = command.ExistingPayPalCustomerId is not null
                ? new Customer { Id = command.ExistingPayPalCustomerId, MerchantCustomerId = command.MerchantBuyerId }
                : new Customer { MerchantCustomerId = command.MerchantBuyerId };

            SetupTokenResponse setupToken;
            try
            {
                setupToken = await _client.Vault.CreateSetupToken(new RequestsVault.CreateSetupTokenRequest
                {
                    Body = new SetupTokenRequest
                    {
                        Customer = customer,
                        PaymentSource = new SetupTokenRequestPaymentSource
                        {
                            Card = new SetupTokenRequestCard
                            {
                                Number = command.Card.Number,
                                Expiry = command.Card.ExpiryYearMonth,
                                SecurityCode = command.Card.SecurityCode,
                                Name = command.Card.CardholderName,
                                BillingAddress = BuildBillingAddress(command.Card)
                            }
                        }
                    }
                }, cancellationToken: cancellationToken);
            }
            catch (ApiException<CreateSetupTokenError> ex)
            {
                if (ex.Error.TryGetError(out var err)) throw MapError(err, (int)ex.StatusCode);
                throw MapFallback(ex.Error, (int)ex.StatusCode);
            }

            PaymentTokenResponse token;
            try
            {
                token = await _client.Vault.CreatePaymentToken(new RequestsVault.CreatePaymentTokenRequest
                {
                    Body = new PaymentTokenRequest
                    {
                        PaymentSource = new PaymentTokenRequestPaymentSource
                        {
                            Token = new VaultTokenRequest { Id = setupToken.Id!, Type = VaultTokenRequestType.SetupToken }
                        },
                        Customer = customer
                    }
                }, cancellationToken: cancellationToken);
            }
            catch (ApiException<CreatePaymentTokenError> ex)
            {
                if (ex.Error.TryGetError(out var err)) throw MapError(err, (int)ex.StatusCode);
                throw MapFallback(ex.Error, (int)ex.StatusCode);
            }

            var card = token.PaymentSource?.Card;
            return new VaultCardResult(
                token.Id!,
                token.Customer?.Id ?? command.ExistingPayPalCustomerId ?? string.Empty,
                card?.Brand?.Value ?? "Unknown",
                card?.LastDigits ?? string.Empty,
                card?.Expiry,
                card?.Name);
        });

    public Task DeleteCardAsync(string vaultId, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            try
            {
                await _client.Vault.DeletePaymentToken(new RequestsVault.DeletePaymentTokenRequest { Id = vaultId }, cancellationToken: cancellationToken);
            }
            catch (ApiException<DeletePaymentTokenError> ex)
            {
                if (ex.Error.TryGetError(out var err)) throw MapError(err, (int)ex.StatusCode);
                throw MapFallback(ex.Error, (int)ex.StatusCode);
            }

            return true;
        });

    public Task<ReconciliationPage> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, int page, int pageSize, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SearchResponse result;
            try
            {
                result = await _client.TransactionSearch.SearchTransactions(new RequestsTxnSearch.SearchTransactionsRequest
                {
                    StartDate = ToTransactionSearchDate(from),
                    EndDate = ToTransactionSearchDate(to),
                    Fields = "transaction_info",
                    BalanceAffectingRecordsOnly = "N",
                    PageSize = pageSize,
                    Page = page
                }, cancellationToken: cancellationToken);
            }
            catch (ApiException<RawError> ex)
            {
                throw MapRaw(ex.Error);
            }

            var transactions = (result.TransactionDetails ?? new List<TransactionDetails>())
                .Select(d => d.TransactionInfo)
                .Where(info => info is not null)
                .Select(info => new ReconciliationTransaction(
                    info!.TransactionId ?? string.Empty,
                    info.TransactionStatus ?? string.Empty,
                    info.TransactionAmount?.Value is not null ? CurrencyFormat.Parse(info.TransactionAmount.Value) : 0m,
                    info.TransactionAmount?.CurrencyCode ?? string.Empty,
                    info.FeeAmount?.Value is not null ? CurrencyFormat.Parse(info.FeeAmount.Value) : null,
                    info.InvoiceId,
                    info.CustomField))
                .ToList();

            return new ReconciliationPage(transactions, page, (int)(result.TotalPages ?? 1));
        });

    private static SdkPaymentSource BuildPaymentSource(DomainPaymentSource source)
    {
        if (source.Card is not null)
        {
            var c = source.Card;
            return new SdkPaymentSource
            {
                Card = new CardRequest
                {
                    Number = c.Number,
                    Expiry = c.ExpiryYearMonth,
                    SecurityCode = c.SecurityCode,
                    Name = c.CardholderName,
                    BillingAddress = BuildBillingAddress(c)
                }
            };
        }

        return new SdkPaymentSource { Card = new CardRequest { VaultId = source.VaultId } };
    }

    /// <summary>
    /// PayPal's own docs for Address.AddressLine1 say this field "needs to pass the full address" for
    /// compliance/risk checks - a bare street alone was observed to make PayPal's risk engine refuse
    /// the transaction (TRANSACTION_REFUSED) even though the request was otherwise well-formed. Compose
    /// a fuller line so a caller supplying just a plain street still gets a passing risk check.
    /// </summary>
    private static SdkAddress BuildBillingAddress(CardDetails c) => new()
    {
        AddressLine1 = $"{c.Street}, {c.City}, {c.State} {c.PostalCode}, {c.Country}".Trim(),
        AdminArea2 = c.City,
        AdminArea1 = c.State,
        PostalCode = c.PostalCode,
        CountryCode = c.Country
    };

    private static string? DescribePaymentSource(DomainPaymentSource source) =>
        source.Card is not null
            ? $"card ending {(source.Card.Number.Length >= 4 ? source.Card.Number[^4..] : source.Card.Number)}"
            : $"saved card {source.VaultId}";

    private static void EnsureNoChallenge(OrderStatus? status)
    {
        if (status is not null && status == OrderStatus.PayerActionRequired)
        {
            throw new PaymentChallengeRequiredException();
        }
    }

    private static bool IsAuthorizationStale(Error err)
    {
        var name = err.Name ?? string.Empty;
        var message = err.Message ?? string.Empty;
        return name.Contains("EXPIRED", StringComparison.OrdinalIgnoreCase)
            || message.Contains("expired", StringComparison.OrdinalIgnoreCase)
            || name.Contains("AUTHORIZATION_EXPIRED", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// PayPal's transaction-search date fields reject .NET's round-trip ("O") format - its 7-digit
    /// fractional seconds trip "Invalid date passed" (confirmed against the live sandbox). Format
    /// without sub-second precision instead: "yyyy-MM-ddTHH:mm:sszzz".
    /// </summary>
    private static string ToTransactionSearchDate(DateTimeOffset value) =>
        value.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    private static string? ToExpiryString(object? value) => value switch
    {
        null => null,
        string s => s,
        DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    private PayPalGatewayException MapCaseA(CreateOrderError error, int statusCode)
    {
        if (error.TryGetError(out var err)) return MapError(err, statusCode);
        return MapFallback(error, statusCode);
    }

    private PayPalGatewayException MapCaseA(AuthorizeOrderError error, int statusCode)
    {
        if (error.TryGetError(out var err)) return MapError(err, statusCode);
        return MapFallback(error, statusCode);
    }

    private PayPalGatewayException MapError(Error err, int statusCode)
    {
        _logger.LogWarning(
            "PayPal rejected a request: name={Name} message={Message} debug_id={DebugId} status={StatusCode} details={Details}",
            err.Name, err.Message, err.DebugId, statusCode, System.Text.Json.JsonSerializer.Serialize(err.Details));
        return new(string.IsNullOrWhiteSpace(err.Message) ? "PayPal rejected the request." : err.Message!, statusCode, err.DebugId);
    }

    private PayPalGatewayException MapRaw(RawError raw)
    {
        _logger.LogWarning("PayPal returned a raw error: status={StatusCode} body={Body}", (int)raw.StatusCode, raw.ReadAsString());
        return new($"PayPal returned HTTP {(int)raw.StatusCode}.", (int)raw.StatusCode, null);
    }

    private PayPalGatewayException MapFallback(ApiError error, int statusCode)
    {
        if (error.TryGetRawError(out var raw)) return MapRaw(raw);
        return new PayPalGatewayException("PayPal returned an unrecognised error.", statusCode, null);
    }

    private async Task<T> ExecuteAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (PaymentException)
        {
            throw; // already a caller-safe domain exception (AuthorizationStaleException's parent isn't PaymentException - see below)
        }
        catch (ResponseDeserializationException ex)
        {
            throw new PayPalGatewayException(
                "PayPal returned a response that could not be processed.",
                (int)ex.StatusCode,
                null,
                isOutcomeUnknown: ex.StatusCode == HttpStatusCode.OK);
        }
        catch (SdkTimeoutException ex)
        {
            throw new PayPalGatewayException($"PayPal did not respond within {ex.Timeout}.", ex);
        }
        catch (SdkConnectionException ex)
        {
            throw new PayPalGatewayException("Could not reach PayPal.", ex);
        }
        catch (AuthSchemeException ex)
        {
            throw new PayPalGatewayException("PayPal rejected this application's credentials.", ex, isOutcomeUnknown: false);
        }
    }
}
