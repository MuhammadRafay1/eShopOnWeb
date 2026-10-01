using System;
using System.Collections.Generic;
using System.Linq;
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
using PayPalServerSdk.Requests.Orders;
using PayPalServerSdk.Requests.Payments;
using PpMoney = PayPalServerSdk.Models.Money;
using Money = Microsoft.eShopWeb.ApplicationCore.Payments.Money;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// PayPal implementation of <see cref="IPaymentGateway"/>. Translates ApplicationCore DTOs to/from the PayPal
/// SDK and owns all error handling and the deterministic PayPal-Request-Id scheme. No PayPal SDK type crosses
/// back into ApplicationCore. Writes are re-issued once under the same request-id on a transport failure, which
/// settles the unknown outcome via PayPal's own idempotency dedup.
/// </summary>
public sealed class PayPalGateway : IPaymentGateway
{
    private static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(30);

    // Per-process scope for the deterministic PayPal-Request-Id. Within one running process the id is stable per
    // (order, operation) so a double-click dedups at PayPal; across a process restart it differs, which matters
    // here because the in-memory database resets order ids on restart — without this, a new run's "order-1" would
    // collide with a prior run's request-id at PayPal. In a persistent-DB deployment order ids never repeat, so
    // this only ever adds safety.
    private static readonly string InstanceId = Guid.NewGuid().ToString("N").Substring(0, 8);

    private readonly PayPalServerSdkClient _client;
    private readonly IAppLogger<PayPalGateway> _logger;

    private static string RequestId(string orderReference, string operation) => $"{orderReference}-{operation}-{InstanceId}";

    public PayPalGateway(PayPalServerSdkClient client, IAppLogger<PayPalGateway> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<AuthorizationResult> AuthorizeAsync(PaymentAuthorizationRequest request, CancellationToken cancellationToken)
    {
        var card = BuildCardRequest(request);
        var createRequest = new CreateOrderRequest
        {
            PayPalRequestId = RequestId(request.OrderReference, "authorize"),
            Body = new OrderRequest
            {
                Intent = CheckoutPaymentIntent.Authorize,
                PurchaseUnits = new List<PurchaseUnitRequest>
                {
                    new()
                    {
                        Amount = new AmountWithBreakdown
                        {
                            CurrencyCode = request.Amount.CurrencyCode,
                            Value = MoneyFormatter.ToWire(request.Amount.Amount)
                        }
                    }
                },
                PaymentSource = new PaymentSource { Card = card }
            }
        };

        try
        {
            var order = await SendAsync(ct => _client.Orders.CreateOrder(createRequest, cancellationToken: ct), cancellationToken);

            if (order.Status is { } status && status == PayPalServerSdk.Models.Enums.OrderStatus.PayerActionRequired)
            {
                throw new PayerActionRequiredException();
            }

            var authorization = ExtractAuthorization(order.PurchaseUnits);
            if (authorization is null)
            {
                // CreateOrder did not finish the authorization synchronously — do the explicit authorize step.
                var authorizeRequest = new AuthorizeOrderRequest
                {
                    Id = order.Id!,
                    PayPalRequestId = RequestId(request.OrderReference, "authorize-step")
                };
                var authorizeResponse = await SendAsync(ct => _client.Orders.AuthorizeOrder(authorizeRequest, cancellationToken: ct), cancellationToken);
                authorization = ExtractAuthorization(authorizeResponse.PurchaseUnits);
            }

            if (authorization?.Id is null)
            {
                throw new PaymentGatewayException("PayPal accepted the order but returned no authorization to act on.", null);
            }

            return new AuthorizationResult(
                order.Id!,
                authorization.Id,
                authorization.Status?.Value ?? "CREATED",
                ParseDate(authorization.ExpirationTime));
        }
        catch (ApiException<CreateOrderError> ex)
        {
            throw TranslateTyped(ex, ex.Error.TryGetError(out var e) ? e : null);
        }
        catch (ApiException<AuthorizeOrderError> ex)
        {
            throw TranslateTyped(ex, ex.Error.TryGetError(out var e) ? e : null);
        }
        catch (Exception ex) when (IsTransportOrShape(ex))
        {
            throw TranslateCommon(ex);
        }
    }

    public async Task<CaptureResult> CaptureAsync(string orderReference, string authorizationId, Money amount, CancellationToken cancellationToken)
    {
        var request = new CaptureAuthorizedPaymentRequest
        {
            AuthorizationId = authorizationId,
            PayPalRequestId = RequestId(orderReference, "capture"),
            // Full representation so PayPal returns the seller_receivable_breakdown (fee + net proceeds).
            Prefer = "return=representation",
            Body = new CaptureRequest
            {
                Amount = new PpMoney { CurrencyCode = amount.CurrencyCode, Value = MoneyFormatter.ToWire(amount.Amount) },
                FinalCapture = true
            }
        };

        try
        {
            var captured = await SendAsync(ct => _client.Payments.CaptureAuthorizedPayment(request, cancellationToken: ct), cancellationToken);
            return new CaptureResult(
                captured.Id!,
                captured.Status?.Value ?? "COMPLETED",
                MoneyFormatter.ParseOrNull(captured.Amount?.Value) ?? amount.Amount,
                MoneyFormatter.ParseOrNull(captured.SellerReceivableBreakdown?.PaypalFee?.Value),
                MoneyFormatter.ParseOrNull(captured.SellerReceivableBreakdown?.NetAmount?.Value));
        }
        catch (ApiException<CaptureAuthorizedPaymentError> ex)
        {
            if (ex.Error.TryGetError(out var err))
            {
                if (IsAuthorizationExpired(err))
                {
                    throw new AuthorizationExpiredException(err.Message, ex);
                }
                throw TranslateTyped(ex, err);
            }
            throw TranslateTyped(ex, null);
        }
        catch (Exception ex) when (IsTransportOrShape(ex))
        {
            throw TranslateCommon(ex);
        }
    }

    public async Task<ReauthorizationResult> ReauthorizeAsync(string orderReference, string authorizationId, Money amount, CancellationToken cancellationToken)
    {
        var request = new ReauthorizePaymentRequest
        {
            AuthorizationId = authorizationId,
            PayPalRequestId = RequestId(orderReference, "reauth"),
            Body = new ReauthorizeRequest
            {
                Amount = new PpMoney { CurrencyCode = amount.CurrencyCode, Value = MoneyFormatter.ToWire(amount.Amount) }
            }
        };

        try
        {
            var auth = await SendAsync(ct => _client.Payments.ReauthorizePayment(request, cancellationToken: ct), cancellationToken);
            return new ReauthorizationResult(
                auth.Id!,
                auth.Status?.Value ?? "CREATED",
                ParseDate(auth.ExpirationTime));
        }
        catch (ApiException<ReauthorizePaymentError> ex)
        {
            var reason = ex.Error.TryGetError(out var err)
                ? DescribeError(err)
                : $"HTTP {(int)ex.StatusCode}";
            _logger.LogWarning("Reauthorization failed for {0}: {1}", orderReference, reason);
            throw new ReauthorizationFailedException(
                $"The authorization for this order could not be renewed and the payment cannot be captured ({reason}). " +
                "The hold has likely expired beyond the window PayPal allows; place a new order to collect payment.");
        }
        catch (Exception ex) when (IsTransportOrShape(ex))
        {
            throw TranslateCommon(ex);
        }
    }

    public async Task VoidAsync(string orderReference, string authorizationId, CancellationToken cancellationToken)
    {
        var request = new VoidPaymentRequest
        {
            AuthorizationId = authorizationId,
            PayPalRequestId = RequestId(orderReference, "void")
        };

        try
        {
            await SendAsync(ct => _client.Payments.VoidPayment(request, cancellationToken: ct), cancellationToken);
        }
        catch (ApiException<VoidPaymentError> ex)
        {
            // 409 means the authorization is already voided or captured — the target state, so idempotent success.
            if ((int)ex.StatusCode == 409)
            {
                _logger.LogInformation("Void of {0} returned 409 (already voided/captured) — treated as idempotent success.", authorizationId);
                return;
            }
            throw TranslateTyped(ex, ex.Error.TryGetError(out var err) ? err : null);
        }
        catch (ResponseDeserializationException ex) when ((int)ex.StatusCode is 200 or 204)
        {
            // PayPal answers a successful void with 204 No Content; the SDK declares a body, so the empty body
            // surfaces as a deserialization error. The void succeeded — nothing to read.
            _logger.LogInformation("Void of {0} succeeded ({1} No Content).", authorizationId, (int)ex.StatusCode);
            return;
        }
        catch (Exception ex) when (IsTransportOrShape(ex))
        {
            throw TranslateCommon(ex);
        }
    }

    public async Task<RefundResult> RefundAsync(string captureId, Money? amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        var request = new RefundCapturedPaymentRequest
        {
            CaptureId = captureId,
            PayPalRequestId = idempotencyKey,
            Prefer = "return=representation",
            Body = amount is { } m
                ? new RefundRequest { Amount = new PpMoney { CurrencyCode = m.CurrencyCode, Value = MoneyFormatter.ToWire(m.Amount) } }
                : null
        };

        try
        {
            var refund = await SendAsync(ct => _client.Payments.RefundCapturedPayment(request, cancellationToken: ct), cancellationToken);
            return new RefundResult(
                refund.Id!,
                refund.Status?.Value ?? "PENDING",
                MoneyFormatter.ParseOrNull(refund.Amount?.Value) ?? amount?.Amount ?? 0m);
        }
        catch (ApiException<RefundCapturedPaymentError> ex)
        {
            throw TranslateTyped(ex, ex.Error.TryGetError(out var err) ? err : null);
        }
        catch (Exception ex) when (IsTransportOrShape(ex))
        {
            throw TranslateCommon(ex);
        }
    }

    public async Task<OrderSnapshot> GetOrderSnapshotAsync(string payPalOrderId, CancellationToken cancellationToken)
    {
        try
        {
            var order = await SendAsync(ct => _client.Orders.GetOrder(new GetOrderRequest { Id = payPalOrderId }, cancellationToken: ct), cancellationToken);
            var authorization = ExtractAuthorization(order.PurchaseUnits);
            var capture = order.PurchaseUnits?.FirstOrDefault()?.Payments?.Captures?.FirstOrDefault();

            return new OrderSnapshot(
                payPalOrderId,
                order.Status?.Value,
                authorization?.Id,
                authorization?.Status?.Value,
                ParseDate(authorization?.ExpirationTime),
                capture?.Id,
                capture?.Status?.Value,
                MoneyFormatter.ParseOrNull(capture?.Amount?.Value),
                MoneyFormatter.ParseOrNull(capture?.SellerReceivableBreakdown?.PaypalFee?.Value),
                MoneyFormatter.ParseOrNull(capture?.SellerReceivableBreakdown?.NetAmount?.Value));
        }
        catch (ApiException<GetOrderError> ex)
        {
            throw TranslateTyped(ex, ex.Error.TryGetError(out var err) ? err : null);
        }
        catch (Exception ex) when (IsTransportOrShape(ex))
        {
            throw TranslateCommon(ex);
        }
    }

    // ---- helpers ----

    private CardRequest BuildCardRequest(PaymentAuthorizationRequest request)
    {
        if (request.VaultTokenId is not null)
        {
            return new CardRequest { VaultId = request.VaultTokenId };
        }

        var card = request.Card
            ?? throw new BadPaymentRequestException("A card or a saved payment method is required to authorize a payment.");

        return new CardRequest
        {
            Name = card.Name,
            Number = card.Number,
            Expiry = card.Expiry,
            SecurityCode = card.SecurityCode,
            BillingAddress = BuildAddress(card.BillingAddress)
        };
    }

    private static PayPalServerSdk.Models.Address? BuildAddress(CardBillingAddress? address)
    {
        if (address is null)
        {
            return null;
        }
        return new PayPalServerSdk.Models.Address
        {
            AddressLine1 = address.AddressLine1,
            AddressLine2 = address.AddressLine2,
            AdminArea2 = address.AdminArea2,
            AdminArea1 = address.AdminArea1,
            PostalCode = address.PostalCode,
            CountryCode = address.CountryCode
        };
    }

    private static AuthorizationWithAdditionalData? ExtractAuthorization(IReadOnlyList<PurchaseUnit>? purchaseUnits) =>
        purchaseUnits?.FirstOrDefault()?.Payments?.Authorizations?.FirstOrDefault();

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;

    private static bool IsAuthorizationExpired(Error error) =>
        error.Details?.Any(d => string.Equals(d.Issue, "AUTHORIZATION_EXPIRED", StringComparison.OrdinalIgnoreCase)) == true;

    private static string DescribeError(Error error)
    {
        var issue = error.Details?.FirstOrDefault()?.Issue;
        return issue is null ? $"{error.Name}: {error.Message}" : $"{error.Name} ({issue}): {error.Message}";
    }

    private PaymentGatewayException TranslateTyped(ApiException ex, Error? error)
    {
        if (error is not null)
        {
            _logger.LogWarning("PayPal error {0} (debug_id={1}) on {2} {3}", DescribeError(error), error.DebugId, ex.Method, ex.RequestUri);
            return new PaymentGatewayException($"PayPal rejected the request: {DescribeError(error)}", (int)ex.StatusCode, error.DebugId, ex);
        }

        _logger.LogWarning("PayPal error HTTP {0} on {1} {2}", (int)ex.StatusCode, ex.Method, ex.RequestUri);
        return new PaymentGatewayException($"PayPal returned an error (HTTP {(int)ex.StatusCode}).", (int)ex.StatusCode, inner: ex);
    }

    private PaymentGatewayException TranslateCommon(Exception ex)
    {
        switch (ex)
        {
            case ResponseDeserializationException rde:
                _logger.LogWarning("PayPal returned an unprocessable response (status {0}) on {1}.", (int)rde.StatusCode, rde.RequestUri);
                return new PaymentGatewayException("PayPal returned a response that could not be processed.", (int)rde.StatusCode, inner: rde);
            case SdkTimeoutException te:
                _logger.LogWarning("PayPal timed out on {0}.", te.RequestUri);
                return new PaymentGatewayException("PayPal did not respond in time.", null, inner: te);
            case SdkConnectionException ce:
                _logger.LogWarning("PayPal was unreachable on {0}.", ce.RequestUri);
                return new PaymentGatewayException("PayPal is currently unreachable.", null, inner: ce);
            case AuthSchemeException ae:
                _logger.LogWarning("PayPal credentials could not be applied.");
                return new PaymentGatewayException("PayPal credentials were rejected.", null, inner: ae);
            default:
                return new PaymentGatewayException("An unexpected PayPal error occurred.", null, inner: ex);
        }
    }

    private static bool IsTransportOrShape(Exception ex) =>
        ex is ResponseDeserializationException or SdkTimeoutException or SdkConnectionException or AuthSchemeException;

    /// <summary>
    /// Runs a write bounded by a 30s call budget (linked to the caller's token), re-issuing once under the same
    /// request-id on a transport failure so an unknown outcome is settled by PayPal's own dedup.
    /// </summary>
    private static async Task<T> SendAsync<T>(Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        try
        {
            return await Bounded(call, cancellationToken);
        }
        catch (SdkConnectionException)
        {
            // Covers SdkTimeoutException too (its subtype). Re-issue once under the same request-id to settle the
            // unknown outcome via PayPal's own dedup; if the retry also fails it propagates to the typed boundary.
            return await Bounded(call, cancellationToken);
        }
    }

    private static async Task<T> Bounded<T>(Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(CallBudget);
        return await call(cts.Token);
    }
}
