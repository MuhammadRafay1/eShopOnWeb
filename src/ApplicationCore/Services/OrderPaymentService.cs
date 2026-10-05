using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public sealed record PayOrderCommand(int OrderId, string BuyerId, CardDetails? Card, int? PaymentMethodId);

public sealed record RefundOrderCommand(int OrderId, string CallerId, bool CallerIsOperator, string? IdempotencyKey, decimal? Amount);

public sealed record RefundOutcome(Order Order, PaymentRefund Refund, bool Replayed);

/// <summary>
/// Moves money for an order: authorize (hold) at payment, capture at fulfilment, void on cancel, refund after
/// fulfilment. Every provider write follows claim → provider call → record result, so a double-click or a
/// retry never authorizes, captures or refunds twice, and a write whose outcome is unknown is settled by
/// re-reading the provider before anything is re-issued.
/// </summary>
public class OrderPaymentService
{
    /// <summary>Authorizations are honoured for three days; after that they are reauthorized before capture.</summary>
    public static readonly TimeSpan HonorPeriod = TimeSpan.FromDays(3);

    /// <summary>Fallback validity when the provider does not report an expiry: 29 days from the original hold.</summary>
    public static readonly TimeSpan AuthorizationValidity = TimeSpan.FromDays(29);

    private static readonly Regex IdempotencyKeyPattern = new("^[A-Za-z0-9_.:-]{1,64}$", RegexOptions.Compiled);

    private readonly IPaymentStore _store;
    private readonly IPaymentGateway _gateway;
    private readonly PaymentClaimCoordinator _claims;
    private readonly TimeProvider _clock;
    private readonly IAppLogger<OrderPaymentService> _logger;

    public OrderPaymentService(IPaymentStore store, IPaymentGateway gateway, PaymentClaimCoordinator claims,
        TimeProvider clock, IAppLogger<OrderPaymentService> logger)
    {
        _store = store;
        _gateway = gateway;
        _claims = claims;
        _clock = clock;
        _logger = logger;
    }

    public static bool IsValidIdempotencyKey(string? key) => key is not null && IdempotencyKeyPattern.IsMatch(key);

    // =================================================================== pay (authorize)

    public async Task<Order> PayAsync(PayOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(command.OrderId, command.BuyerId, allowOperator: false, cancellationToken);

        if (order.Status == OrderStatus.PaymentAuthorized || IsFulfilledOrLater(order.Status))
            return order; // already paid: a repeated click gets the same answer and no second hold
        if (order.Status != OrderStatus.AwaitingPayment)
            throw new PaymentStateException($"Order {order.Id} cannot be paid because it is {order.Status}.");
        if (order.Total() <= 0m)
            throw new PaymentStateException($"Order {order.Id} has nothing to pay.");

        var instrument = await ResolveInstrumentAsync(command, cancellationToken);

        var key = PaymentClaimCoordinator.AuthorizeKey(order.Id);
        var acquired = await _claims.AcquireAsync(key, "payment", command.BuyerId, order.Id, cancellationToken);
        if (acquired.AlreadySucceeded)
        {
            order = await ReloadAsync(order.Id, cancellationToken);
            if (order.Status != OrderStatus.AwaitingPayment)
                return order;
            // The earlier hold was given up (it expired before fulfilment): this is a new payment attempt.
            await _claims.ReleaseAsync(acquired.Claim, cancellationToken);
            acquired = await _claims.AcquireAsync(key, "payment", command.BuyerId, order.Id, cancellationToken);
            if (acquired.AlreadySucceeded)
                return await ReloadAsync(order.Id, cancellationToken);
        }

        var claim = acquired.Claim;
        try
        {
            return await AuthorizeAsync(order, claim, acquired.Resumed, instrument, cancellationToken);
        }
        catch (PaymentProviderException ex) when (ex.ProviderDidNotAct && claim.IsHeld)
        {
            await RecordAuthorizationFailureAsync(order, ex, cancellationToken);
            await _claims.ReleaseAsync(claim, cancellationToken);
            throw;
        }
        catch (Exception) when (claim.IsHeld)
        {
            await _claims.AbandonAsync(claim);
            throw;
        }
    }

    private async Task<Order> AuthorizeAsync(Order order, PaymentOperationClaim claim, bool resumed,
        ResolvedInstrument instrument, CancellationToken cancellationToken)
    {
        ProviderOrder? providerOrder = null;
        if (resumed && order.Payment?.ProviderOrderId is { } knownProviderOrderId)
        {
            // An earlier attempt's outcome is unknown: find out what PayPal holds before doing anything else.
            providerOrder = await _gateway.GetOrderAsync(knownProviderOrderId, cancellationToken);
        }
        else if (!resumed || order.Payment is null || order.Payment.Status != PaymentStatus.Pending)
        {
            order.BeginPayment(_gateway.ProviderName, _gateway.Currency, instrument.SavedPaymentMethodId);
            await _store.SaveOrderAsync(order, cancellationToken);
        }

        if (providerOrder is null)
        {
            // A resumed attempt re-sends the same PayPal-Request-Id, so PayPal answers with the order it already created.
            var create = new CreateAuthorizationOrderCommand(order.Id, order.Total(), order.Payment!.Currency,
                instrument.Instrument, claim.ProviderRequestId + "-create");
            providerOrder = await WriteAsync(claim, order, ct => _gateway.CreateAuthorizationOrderAsync(create, ct),
                settle: null, cancellationToken);
            order.RecordProviderOrder(providerOrder.Id, providerOrder.RawStatus);
            await _store.SaveOrderAsync(order, cancellationToken);
        }

        ThrowIfPayerActionRequired(providerOrder);

        var hold = FindHold(providerOrder);
        if (hold is null)
        {
            var providerOrderId = providerOrder.Id;
            providerOrder = await WriteAsync(claim, order,
                ct => _gateway.AuthorizeOrderAsync(providerOrderId, claim.ProviderRequestId + "-authorize", ct),
                settle: async ct =>
                {
                    var current = await _gateway.GetOrderAsync(providerOrderId, ct);
                    return FindHold(current) is null ? null : current;
                },
                cancellationToken);
            ThrowIfPayerActionRequired(providerOrder);
            hold = FindHold(providerOrder);
        }

        if (hold is null)
            throw new PaymentProviderException(PaymentProviderFailure.Rejected,
                $"PayPal did not place a hold for order {order.Id} (PayPal order status {providerOrder.RawStatus ?? "unknown"}). " +
                "The order is still awaiting payment.");

        if (hold.State == ProviderAuthorizationState.Denied)
            throw new PaymentProviderException(PaymentProviderFailure.Declined,
                $"The card was declined{(hold.StatusReason is null ? "" : $" ({hold.StatusReason})")}. The order is still awaiting payment.");

        if (hold.Amount is { } heldAmount && heldAmount != order.Total())
        {
            // Never keep a hold that does not match the order to the cent.
            await VoidQuietlyAsync(hold.Id, claim.ProviderRequestId + "-void-mismatch", cancellationToken);
            throw new PaymentProviderException(PaymentProviderFailure.Rejected,
                $"PayPal held {heldAmount:0.00} but order {order.Id} totals {order.Total():0.00}; the hold was released.");
        }

        order.RecordProviderOrder(providerOrder.Id, providerOrder.RawStatus);
        order.RecordAuthorization(hold.Id, hold.RawStatus, hold.Amount ?? order.Total(), hold.CreatedAt ?? _clock.GetUtcNow(),
            hold.ExpiresAt, providerOrder.CardBrand, providerOrder.CardLastDigits);
        await _store.SaveOrderAsync(order, cancellationToken);
        await _claims.SucceedAsync(claim, hold.Id, cancellationToken);

        _logger.LogInformation("Order {OrderId} authorized: PayPal order {ProviderOrderId}, authorization {AuthorizationId}, {Amount} {Currency}",
            order.Id, providerOrder.Id, hold.Id, hold.Amount ?? order.Total(), order.Payment!.Currency);
        return order;
    }

    private async Task RecordAuthorizationFailureAsync(Order order, PaymentProviderException reason, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Order {OrderId} payment not authorized ({Failure}): {Reason} [PayPal debug id {DebugId}]",
            order.Id, reason.Failure, reason.Message, reason.ProviderDebugId ?? "-");
        if (order.Payment is null) return;
        var status = reason.Failure == PaymentProviderFailure.PayerActionRequired ? PaymentStatus.ActionRequired : PaymentStatus.Declined;
        order.RecordPaymentFailure(status, reason.Message);
        await _store.SaveOrderAsync(order, cancellationToken);
    }

    private static void ThrowIfPayerActionRequired(ProviderOrder providerOrder)
    {
        if (providerOrder.State == ProviderOrderState.PayerActionRequired)
            throw new PaymentProviderException(PaymentProviderFailure.PayerActionRequired,
                "PayPal requires the shopper to complete a card authentication challenge (3-D Secure) in a browser, " +
                "which this API does not support. The order is still awaiting payment.");
    }

    private static ProviderAuthorization? FindHold(ProviderOrder providerOrder) =>
        providerOrder.Authorizations.FirstOrDefault(a => a.State is ProviderAuthorizationState.Created or ProviderAuthorizationState.Pending)
        ?? providerOrder.Authorizations.FirstOrDefault();

    private sealed record ResolvedInstrument(PaymentInstrument Instrument, int? SavedPaymentMethodId);

    private async Task<ResolvedInstrument> ResolveInstrumentAsync(PayOrderCommand command, CancellationToken cancellationToken)
    {
        if ((command.Card is null) == (command.PaymentMethodId is null))
            throw new PaymentValidationException("Provide either card details or a saved paymentMethodId — exactly one of them.");

        if (command.Card is not null)
            return new ResolvedInstrument(new CardInstrument(CardValidator.Validate(command.Card, _clock.GetUtcNow())), null);

        // A saved card is usable only by the shopper who saved it, and only until it is removed.
        var method = await _store.GetSavedPaymentMethodAsync(command.PaymentMethodId!.Value, command.BuyerId, cancellationToken);
        if (method is null || !method.IsActive)
            throw new PaymentResourceNotFoundException($"Saved payment method {command.PaymentMethodId} was not found.");
        return new ResolvedInstrument(new VaultedCardInstrument(method.VaultTokenId), method.Id);
    }

    // =================================================================== fulfil (capture)

    public async Task<Order> FulfilAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, callerId: null, allowOperator: true, cancellationToken);
        if (IsFulfilledOrLater(order.Status))
            return order;
        if (order.Status != OrderStatus.PaymentAuthorized)
            throw new PaymentStateException(
                $"Order {order.Id} cannot be fulfilled because it is {order.Status}: there is no authorized payment to capture." +
                (order.Payment?.LastFailure is { } last ? $" Last payment problem: {last}" : ""));

        var acquired = await _claims.AcquireAsync(PaymentClaimCoordinator.CaptureKey(order.Id), "capture", order.BuyerId,
            order.Id, cancellationToken);
        if (acquired.AlreadySucceeded)
            return await ReloadAsync(order.Id, cancellationToken);

        var claim = acquired.Claim;
        try
        {
            return await CaptureAsync(order, claim, acquired.Resumed, cancellationToken);
        }
        catch (PaymentProviderException ex) when (ex.ProviderDidNotAct && claim.IsHeld)
        {
            order.RecordPaymentNote($"Capture failed: {ex.Message}");
            await _store.SaveOrderAsync(order, cancellationToken);
            await _claims.ReleaseAsync(claim, cancellationToken);
            _logger.LogWarning("Order {OrderId} capture refused by PayPal: {Reason} [PayPal debug id {DebugId}]",
                order.Id, ex.Message, ex.ProviderDebugId ?? "-");
            throw;
        }
        catch (Exception) when (claim.IsHeld)
        {
            await _claims.AbandonAsync(claim);
            throw;
        }
    }

    private async Task<Order> CaptureAsync(Order order, PaymentOperationClaim claim, bool resumed, CancellationToken cancellationToken)
    {
        var payment = order.Payment!;
        if (resumed && await FindCaptureAsync(payment, cancellationToken) is { } earlierCapture)
            return await CompleteCaptureAsync(order, claim, earlierCapture, cancellationToken);

        var authorization = await _gateway.GetAuthorizationAsync(payment.AuthorizationId!, cancellationToken);
        if (authorization.State is ProviderAuthorizationState.Captured or ProviderAuthorizationState.PartiallyCaptured
            && await FindCaptureAsync(payment, cancellationToken) is { } existingCapture)
            return await CompleteCaptureAsync(order, claim, existingCapture, cancellationToken);

        var authorizationId = await EnsureCapturableAsync(order, claim, authorization, cancellationToken);
        var amount = payment.AuthorizedAmount ?? payment.Amount;

        var capture = await WriteAsync(claim, order,
            ct => _gateway.CaptureAsync(authorizationId, amount, payment.Currency, claim.ProviderRequestId + "-capture", ct),
            settle: ct => FindCaptureAsync(payment, ct),
            cancellationToken);

        if (capture.State is ProviderCaptureState.Declined or ProviderCaptureState.Failed)
            throw new PaymentProviderException(PaymentProviderFailure.Declined,
                $"PayPal {capture.RawStatus?.ToLowerInvariant() ?? "declined"} the capture of authorization {authorizationId}. " +
                "No money was taken; the order is still awaiting fulfilment.");

        if (capture.Fee is null || capture.Net is null)
            capture = await RefreshCaptureAsync(capture, cancellationToken);

        return await CompleteCaptureAsync(order, claim, capture, cancellationToken);
    }

    /// <summary>
    /// Makes sure the hold can still be captured: a hold past its honor period is renewed (reauthorized) first;
    /// one that can no longer be renewed is given up with a message an operator can act on.
    /// </summary>
    private async Task<string> EnsureCapturableAsync(Order order, PaymentOperationClaim claim,
        ProviderAuthorization authorization, CancellationToken cancellationToken)
    {
        var payment = order.Payment!;
        order.RecordAuthorizationStatus(authorization.RawStatus);
        var now = _clock.GetUtcNow();

        if (authorization.State is not (ProviderAuthorizationState.Created or ProviderAuthorizationState.Pending))
            await GiveUpHoldAsync(order, claim,
                $"The payment authorization {authorization.Id} is {authorization.RawStatus ?? "no longer active"} at PayPal and cannot be captured.",
                cancellationToken);

        var authorizedAt = payment.AuthorizedAt ?? authorization.CreatedAt ?? now;
        var expiresAt = authorization.ExpiresAt ?? payment.AuthorizationExpiresAt ?? authorizedAt + AuthorizationValidity;
        if (expiresAt <= now)
            await GiveUpHoldAsync(order, claim,
                $"The payment authorization {authorization.Id} expired on {expiresAt:u} and can no longer be renewed.",
                cancellationToken);

        if (now - authorizedAt < HonorPeriod)
            return authorization.Id;

        // Past the honor period: renew the hold before capturing it.
        ProviderAuthorization renewed;
        try
        {
            var amount = payment.AuthorizedAmount ?? payment.Amount;
            renewed = await WriteAsync(claim, order,
                ct => _gateway.ReauthorizeAsync(authorization.Id, amount, payment.Currency,
                    claim.ProviderRequestId + "-reauthorize-" + payment.ReauthorizationCount, ct),
                settle: null, cancellationToken);
        }
        catch (PaymentProviderException ex) when (ex.ProviderDidNotAct)
        {
            await GiveUpHoldAsync(order, claim,
                $"The payment authorization {authorization.Id} is past its {HonorPeriod.TotalDays:0}-day honor period and PayPal " +
                $"refused to renew it ({ex.Message}{(ex.ProviderDebugId is null ? "" : $"; PayPal debug id {ex.ProviderDebugId}")}).",
                cancellationToken);
            throw; // unreachable: GiveUpHoldAsync always throws
        }

        if (renewed.State is not (ProviderAuthorizationState.Created or ProviderAuthorizationState.Pending))
            await GiveUpHoldAsync(order, claim,
                $"PayPal did not renew the payment authorization {authorization.Id} (renewal status {renewed.RawStatus ?? "unknown"}).",
                cancellationToken);

        order.RecordReauthorization(renewed.Id, renewed.RawStatus, renewed.Amount ?? payment.AuthorizedAmount,
            renewed.CreatedAt ?? now, renewed.ExpiresAt);
        await _store.SaveOrderAsync(order, cancellationToken);
        _logger.LogInformation("Order {OrderId}: authorization {OldAuthorizationId} renewed as {AuthorizationId} before capture",
            order.Id, authorization.Id, renewed.Id);
        return renewed.Id;
    }

    /// <summary>The hold cannot be captured any more: return the order to AwaitingPayment and tell the operator what to do.</summary>
    private async Task GiveUpHoldAsync(Order order, PaymentOperationClaim claim, string reason, CancellationToken cancellationToken)
    {
        var message = $"{reason} No money was taken. To fulfil order {order.Id} the shopper must pay again " +
                      $"(POST /api/orders/{order.Id}/pay); the order has been returned to AwaitingPayment.";
        order.RecordPaymentFailure(PaymentStatus.AuthorizationExpired, message);
        await _store.SaveOrderAsync(order, cancellationToken);
        await _claims.ReleaseAsync(claim, cancellationToken);
        if (await _store.GetClaimAsync(PaymentClaimCoordinator.AuthorizeKey(order.Id), cancellationToken) is { } authorizeClaim)
            await _claims.ReleaseAsync(authorizeClaim, cancellationToken);
        _logger.LogWarning("Order {OrderId}: authorization given up: {Reason}", order.Id, reason);
        throw new PaymentStateException(message);
    }

    private async Task<ProviderCapture?> FindCaptureAsync(OrderPayment payment, CancellationToken cancellationToken)
    {
        if (payment.ProviderOrderId is null) return null;
        var providerOrder = await _gateway.GetOrderAsync(payment.ProviderOrderId, cancellationToken);
        var capture = providerOrder.Captures.FirstOrDefault(c => c.State is not (ProviderCaptureState.Declined or ProviderCaptureState.Failed));
        if (capture is null) return null;
        return capture.Fee is null || capture.Net is null ? await RefreshCaptureAsync(capture, cancellationToken) : capture;
    }

    private async Task<ProviderCapture> RefreshCaptureAsync(ProviderCapture capture, CancellationToken cancellationToken)
    {
        try
        {
            return await _gateway.GetCaptureAsync(capture.Id, cancellationToken);
        }
        catch (PaymentProviderException ex)
        {
            // The capture itself is confirmed; only the fee breakdown is missing. Record what we have.
            _logger.LogWarning("Capture {CaptureId}: could not read fee breakdown yet: {Reason}", capture.Id, ex.Message);
            return capture;
        }
    }

    private async Task<Order> CompleteCaptureAsync(Order order, PaymentOperationClaim claim, ProviderCapture capture,
        CancellationToken cancellationToken)
    {
        order.RecordCapture(capture.Id, capture.RawStatus, capture.Amount, capture.Fee, capture.Net,
            capture.CreatedAt ?? _clock.GetUtcNow());
        await _store.SaveOrderAsync(order, cancellationToken);
        await _claims.SucceedAsync(claim, capture.Id, cancellationToken);
        _logger.LogInformation("Order {OrderId} fulfilled: capture {CaptureId} {Amount} {Currency}, PayPal fee {Fee}, net {Net}",
            order.Id, capture.Id, capture.Amount ?? 0m, capture.Currency ?? order.Payment!.Currency, capture.Fee ?? 0m, capture.Net ?? 0m);
        return order;
    }

    // =================================================================== cancel (void)

    public async Task<Order> CancelAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, callerId: null, allowOperator: true, cancellationToken);
        if (order.Status == OrderStatus.Cancelled)
            return order;
        if (IsFulfilledOrLater(order.Status))
            throw new PaymentStateException($"Order {order.Id} has already been fulfilled and paid; refund it instead of cancelling.");

        if (order.Status == OrderStatus.AwaitingPayment)
        {
            if (await _store.GetClaimAsync(PaymentClaimCoordinator.AuthorizeKey(order.Id), cancellationToken) is { State: not PaymentClaimState.Succeeded })
                throw new PaymentConflictException($"A payment for order {order.Id} is in progress; retry the cancellation shortly.");
            order.Cancel(null, _clock.GetUtcNow());
            await _store.SaveOrderAsync(order, cancellationToken);
            return order;
        }

        if (await _store.GetClaimAsync(PaymentClaimCoordinator.CaptureKey(order.Id), cancellationToken) is not null)
            throw new PaymentConflictException($"Order {order.Id} is being fulfilled; it can no longer be cancelled.");

        var acquired = await _claims.AcquireAsync(PaymentClaimCoordinator.VoidKey(order.Id), "cancellation", order.BuyerId,
            order.Id, cancellationToken);
        if (acquired.AlreadySucceeded)
            return await ReloadAsync(order.Id, cancellationToken);

        var claim = acquired.Claim;
        var authorizationId = order.Payment!.AuthorizationId!;
        try
        {
            if (acquired.Resumed)
            {
                var current = await _gateway.GetAuthorizationAsync(authorizationId, cancellationToken);
                if (!HoldsFunds(current))
                    return await CompleteCancelAsync(order, claim, current, cancellationToken);
            }

            var voided = await WriteAsync(claim, order,
                ct => _gateway.VoidAsync(authorizationId, claim.ProviderRequestId + "-void", ct),
                settle: async ct =>
                {
                    var current = await _gateway.GetAuthorizationAsync(authorizationId, ct);
                    return HoldsFunds(current) ? null : current;
                },
                cancellationToken);
            return await CompleteCancelAsync(order, claim, voided, cancellationToken);
        }
        catch (PaymentProviderException ex) when (ex.ProviderDidNotAct && claim.IsHeld)
        {
            // PayPal refused the void — perhaps because the hold is already gone (voided or expired). Check.
            ProviderAuthorization? current = null;
            try { current = await _gateway.GetAuthorizationAsync(authorizationId, cancellationToken); }
            catch (PaymentProviderException) { /* fall through and report the refusal */ }

            if (current is not null && !HoldsFunds(current))
                return await CompleteCancelAsync(order, claim, current, cancellationToken);

            await _claims.ReleaseAsync(claim, cancellationToken);
            throw;
        }
        catch (Exception) when (claim.IsHeld)
        {
            await _claims.AbandonAsync(claim);
            throw;
        }
    }

    private static bool HoldsFunds(ProviderAuthorization authorization) =>
        authorization.State is ProviderAuthorizationState.Created or ProviderAuthorizationState.Pending
            or ProviderAuthorizationState.PartiallyCaptured or ProviderAuthorizationState.Captured;

    private async Task<Order> CompleteCancelAsync(Order order, PaymentOperationClaim claim, ProviderAuthorization authorization,
        CancellationToken cancellationToken)
    {
        order.Cancel(authorization.RawStatus, _clock.GetUtcNow());
        await _store.SaveOrderAsync(order, cancellationToken);
        await _claims.SucceedAsync(claim, authorization.Id, cancellationToken);
        _logger.LogInformation("Order {OrderId} cancelled; authorization {AuthorizationId} released ({Status})",
            order.Id, authorization.Id, authorization.RawStatus ?? "-");
        return order;
    }

    // =================================================================== refund

    public async Task<RefundOutcome> RefundAsync(RefundOrderCommand command, CancellationToken cancellationToken)
    {
        if (!IsValidIdempotencyKey(command.IdempotencyKey))
            throw new PaymentValidationException(
                "A refund needs an idempotency key: 1-64 characters of letters, digits, '-', '_', '.' or ':'. " +
                "Repeating a request with the same key never refunds twice.");
        if (command.Amount is { } requested && (requested <= 0m || decimal.Round(requested, 2) != requested))
            throw new PaymentValidationException("The refund amount must be positive with at most two decimal places.");

        var idempotencyKey = command.IdempotencyKey!;
        var order = await LoadOrderAsync(command.OrderId, command.CallerId, command.CallerIsOperator, cancellationToken);

        var acquired = await _claims.AcquireAsync(PaymentClaimCoordinator.RefundKey(order.Id, idempotencyKey), "refund",
            order.BuyerId, order.Id, cancellationToken);
        if (acquired.AlreadySucceeded)
        {
            order = await ReloadAsync(order.Id, cancellationToken);
            var earlier = order.Payment?.Refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey)
                ?? throw new PaymentConflictException($"Idempotency key '{idempotencyKey}' was already used on order {order.Id}.");
            if (command.Amount is { } amount && amount != earlier.Amount)
                throw new PaymentConflictException(
                    $"Idempotency key '{idempotencyKey}' was already used for a refund of {earlier.Amount:0.00}; use a new key for a different refund.");
            return new RefundOutcome(order, earlier, Replayed: true);
        }

        var claim = acquired.Claim;
        PaymentRefund? refund = order.Payment?.Refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);
        try
        {
            if (order.Payment?.CaptureId is null)
                throw new PaymentStateException($"Order {order.Id} cannot be refunded because it is {order.Status}: nothing was captured.");

            var customId = RefundReference(claim);
            if (acquired.Resumed && refund is not null)
            {
                if (await FindRefundAsync(order.Payment, customId, cancellationToken) is { } settled)
                    return await CompleteRefundAsync(order, claim, refund, settled, cancellationToken);
            }
            else if (refund is null)
            {
                refund = order.ReserveRefund(idempotencyKey, command.Amount, _clock.GetUtcNow());
                await _store.SaveOrderAsync(order, cancellationToken);
            }

            var payment = order.Payment;
            var toRefund = refund;
            var result = await WriteAsync(claim, order,
                ct => _gateway.RefundAsync(payment.CaptureId!, toRefund.Amount, payment.Currency, customId,
                    claim.ProviderRequestId + "-refund", ct),
                settle: ct => FindRefundAsync(payment, customId, ct),
                cancellationToken);
            return await CompleteRefundAsync(order, claim, refund, result, cancellationToken);
        }
        catch (PaymentProviderException ex) when (ex.ProviderDidNotAct && claim.IsHeld)
        {
            if (refund is not null)
            {
                order.RecordRefundFailure(refund, ex.Message);
                await _store.SaveOrderAsync(order, cancellationToken);
            }
            await _claims.ReleaseAsync(claim, cancellationToken);
            _logger.LogWarning("Order {OrderId} refund refused by PayPal: {Reason} [PayPal debug id {DebugId}]",
                order.Id, ex.Message, ex.ProviderDebugId ?? "-");
            throw;
        }
        catch (Exception ex) when (claim.IsHeld)
        {
            if (refund is not null && claim.ProviderMayHaveActed)
            {
                try
                {
                    order.RecordRefundOutcomeUnknown(refund, ex.Message);
                    await _store.SaveOrderAsync(order, CancellationToken.None);
                }
                catch (Exception saveFailure)
                {
                    _logger.LogWarning("Order {OrderId}: could not record unknown refund outcome: {Reason}", order.Id, saveFailure.Message);
                }
            }
            await _claims.AbandonAsync(claim);
            throw;
        }
    }

    /// <summary>The reference sent to PayPal as the refund's custom_id, used to find the refund again.</summary>
    private static string RefundReference(PaymentOperationClaim claim) => "eshop-refund-" + claim.ProviderRequestId;

    private async Task<ProviderRefund?> FindRefundAsync(OrderPayment payment, string customId, CancellationToken cancellationToken)
    {
        if (payment.ProviderOrderId is null) return null;
        var providerOrder = await _gateway.GetOrderAsync(payment.ProviderOrderId, cancellationToken);
        return providerOrder.Refunds.FirstOrDefault(r => r.CustomId == customId);
    }

    private async Task<RefundOutcome> CompleteRefundAsync(Order order, PaymentOperationClaim claim, PaymentRefund refund,
        ProviderRefund result, CancellationToken cancellationToken)
    {
        var state = result.State switch
        {
            ProviderRefundState.Completed => RefundState.Completed,
            ProviderRefundState.Pending => RefundState.Pending,
            ProviderRefundState.Failed => RefundState.Failed,
            ProviderRefundState.Cancelled => RefundState.Cancelled,
            _ => RefundState.Pending
        };
        order.RecordRefundResult(refund, result.Id, result.RawStatus, state, result.Amount, result.CreatedAt ?? _clock.GetUtcNow());
        await _store.SaveOrderAsync(order, cancellationToken);
        await _claims.SucceedAsync(claim, result.Id, cancellationToken);
        _logger.LogInformation("Order {OrderId} refund {RefundId}: {Amount} {Currency} ({Status})",
            order.Id, result.Id, refund.Amount, refund.Currency, result.RawStatus ?? "-");
        return new RefundOutcome(order, refund, Replayed: false);
    }

    // =================================================================== helpers

    private async Task<Order> LoadOrderAsync(int orderId, string? callerId, bool allowOperator, CancellationToken cancellationToken)
    {
        var order = await _store.GetOrderAsync(orderId, cancellationToken);
        // Someone else's order is reported exactly like a missing one, so ids cannot be probed.
        if (order is null || (!allowOperator && order.BuyerId != callerId))
            throw new PaymentResourceNotFoundException($"Order {orderId} was not found.");
        return order;
    }

    private async Task<Order> ReloadAsync(int orderId, CancellationToken cancellationToken) =>
        await _store.GetOrderAsync(orderId, cancellationToken)
        ?? throw new PaymentResourceNotFoundException($"Order {orderId} was not found.");

    private static bool IsFulfilledOrLater(OrderStatus status) =>
        status is OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded or OrderStatus.Refunded;

    /// <summary>
    /// Runs one provider write under a claim. If the provider may have acted without confirming, the outcome is
    /// settled right here by <paramref name="settle"/> (a re-read of provider state). If that cannot settle it,
    /// the claim is parked as Unknown — the next request for the same operation settles it before re-issuing
    /// anything, and a re-issue carries the same PayPal-Request-Id — and the caller is told the outcome is unknown.
    /// </summary>
    private async Task<T> WriteAsync<T>(PaymentOperationClaim claim, Order order, Func<CancellationToken, Task<T>> write,
        Func<CancellationToken, Task<T?>>? settle, CancellationToken cancellationToken) where T : class
    {
        claim.MarkProviderMayHaveActed();
        try
        {
            return await write(cancellationToken);
        }
        catch (PaymentProviderException ex) when (!ex.ProviderDidNotAct)
        {
            if (settle is not null)
            {
                try
                {
                    if (await settle(cancellationToken) is { } settled)
                    {
                        _logger.LogInformation("Order {OrderId}: {Operation} outcome was unknown and has been settled from PayPal",
                            order.Id, claim.Operation);
                        return settled;
                    }
                }
                catch (PaymentProviderException settleFailure)
                {
                    _logger.LogWarning("Order {OrderId}: could not settle the {Operation} outcome yet: {Reason}",
                        order.Id, claim.Operation, settleFailure.Message);
                }
            }

            await _claims.MarkUnknownAsync(claim, CancellationToken.None);
            _logger.LogWarning("Order {OrderId}: {Operation} outcome unknown ({Reason}); it will be settled on the next request",
                order.Id, claim.Operation, ex.Message);
            throw new PaymentProviderException(
                ex.Failure == PaymentProviderFailure.Unavailable ? PaymentProviderFailure.OutcomeUnknown : ex.Failure,
                $"{ex.Message} PayPal may or may not have completed the {claim.Operation}; repeat the same request to " +
                "settle it — it will not be performed twice.",
                ex.ProviderStatusCode, ex.ProviderErrorName, ex.ProviderIssue, ex.ProviderDebugId, ex);
        }
    }

    private async Task VoidQuietlyAsync(string authorizationId, string providerRequestId, CancellationToken cancellationToken)
    {
        try
        {
            await _gateway.VoidAsync(authorizationId, providerRequestId, cancellationToken);
        }
        catch (PaymentProviderException ex)
        {
            _logger.LogWarning("Could not void mismatched authorization {AuthorizationId}: {Reason}", authorizationId, ex.Message);
        }
    }
}
