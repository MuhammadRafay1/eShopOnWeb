using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// The PayPal-owned state for a single <see cref="OrderAggregate.Order"/> (one Payment per
/// Order). Its own aggregate root - it holds enough of the state PayPal owns (ids and current
/// status for the hold, the capture and the refunds) that a later request can act on it, and it
/// owns a child <see cref="Refund"/> collection whose members need identity for idempotency
/// lookups. The Order knows nothing of PayPal; it references this by OrderId.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
    public int OrderId { get; private set; }
    public string CurrencyCode { get; private set; }

    /// <summary>
    /// The invoice_id sent to PayPal for this payment. PayPal accounts can require invoice ids to
    /// be globally unique, and the in-memory order id restarts each run, so this is a unique
    /// reference that still traces back to the order (it is the reconciliation join key that
    /// PayPal echoes back verbatim). Null until the first successful authorization.
    /// </summary>
    public string? InvoiceId { get; private set; }

    /// <summary>Must equal Order.Total() at authorize time (amount held to the cent).</summary>
    public decimal AuthorizedAmount { get; private set; }

    /// <summary>The /v2/checkout/orders response id - audit trail only, not acted on directly.</summary>
    public string PayPalOrderId { get; private set; }

    /// <summary>What capture/void/reauthorize act on.</summary>
    public string AuthorizationId { get; private set; }
    public PaymentAuthorizationStatus AuthorizationStatus { get; private set; }
    public DateTimeOffset AuthorizationExpiresAt { get; private set; }
    public int ReauthorizationCount { get; private set; }

    /// <summary>Bumped once per distinct /pay attempt to derive the authorize idempotency key.</summary>
    public int PaymentAttemptCount { get; private set; }

    public string? CaptureId { get; private set; }
    public PaymentCaptureStatus? CaptureStatus { get; private set; }
    public decimal? CapturedGrossAmount { get; private set; }
    public decimal? PayPalFeeAmount { get; private set; }
    public decimal? NetAmount { get; private set; }

    /// <summary>Running total of refunds; never exceeds <see cref="CapturedGrossAmount"/>.</summary>
    public decimal RefundedAmount { get; private set; }

    private readonly List<Refund> _refunds = new();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }
#pragma warning restore CS8618

    /// <summary>
    /// Creates the Payment row for an order that is being paid for the first time, in a
    /// pre-authorization state. The authorization result is then recorded via
    /// <see cref="RecordAuthorization"/> / <see cref="RecordDeniedAuthorization"/>.
    /// </summary>
    public Payment(int orderId, decimal authorizedAmount, string currencyCode)
    {
        Guard.Against.NegativeOrZero(authorizedAmount, nameof(authorizedAmount));
        Guard.Against.NullOrEmpty(currencyCode, nameof(currencyCode));

        OrderId = orderId;
        AuthorizedAmount = authorizedAmount;
        CurrencyCode = currencyCode;
        AuthorizationStatus = PaymentAuthorizationStatus.Pending;
        PayPalOrderId = string.Empty;
        AuthorizationId = string.Empty;
    }

    /// <summary>Bumps and returns the attempt counter used to derive the /pay idempotency key.</summary>
    public int IncrementPaymentAttempt()
    {
        PaymentAttemptCount++;
        return PaymentAttemptCount;
    }

    public void RecordAuthorization(string payPalOrderId, string authorizationId,
        PaymentAuthorizationStatus status, DateTimeOffset expiresAt, string invoiceId)
    {
        Guard.Against.NullOrEmpty(payPalOrderId, nameof(payPalOrderId));
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));
        Guard.Against.NullOrEmpty(invoiceId, nameof(invoiceId));

        PayPalOrderId = payPalOrderId;
        AuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
        InvoiceId = invoiceId;
    }

    public void RecordDeniedAuthorization(string payPalOrderId)
    {
        PayPalOrderId = payPalOrderId ?? string.Empty;
        AuthorizationStatus = PaymentAuthorizationStatus.Denied;
    }

    public void RecordReauthorization(PaymentAuthorizationStatus status, DateTimeOffset newExpiresAt)
    {
        AuthorizationStatus = status;
        AuthorizationExpiresAt = newExpiresAt;
        ReauthorizationCount++;
    }

    public void RecordCapture(string captureId, PaymentCaptureStatus status,
        decimal grossAmount, decimal feeAmount, decimal netAmount)
    {
        Guard.Against.NullOrEmpty(captureId, nameof(captureId));

        CaptureId = captureId;
        CaptureStatus = status;
        CapturedGrossAmount = grossAmount;
        PayPalFeeAmount = feeAmount;
        NetAmount = netAmount;
        AuthorizationStatus = PaymentAuthorizationStatus.Captured;
    }

    public void RecordVoid()
    {
        AuthorizationStatus = PaymentAuthorizationStatus.Voided;
    }

    /// <summary>
    /// The refundable balance left on this capture. Zero until captured.
    /// </summary>
    public decimal RemainingRefundable =>
        (CapturedGrossAmount ?? 0m) - RefundedAmount;

    /// <summary>
    /// Records a refund, guarding that the running total never exceeds the captured gross amount.
    /// </summary>
    public Refund AddRefund(string payPalRefundId, decimal amount, string status, string idempotencyKey)
    {
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));
        Guard.Against.NegativeOrZero(amount, nameof(amount));

        if (CaptureId is null)
        {
            throw new InvalidOrderStateException(
                $"Cannot refund payment for order {OrderId}: nothing has been captured yet.");
        }

        if (amount > RemainingRefundable)
        {
            throw new RefundLimitExceededException(
                $"Refund of {amount} exceeds the remaining refundable balance {RemainingRefundable} for order {OrderId}.");
        }

        var refund = new Refund(payPalRefundId, idempotencyKey, amount, status);
        _refunds.Add(refund);
        RefundedAmount += amount;

        CaptureStatus = RefundedAmount >= CapturedGrossAmount
            ? PaymentCaptureStatus.Refunded
            : PaymentCaptureStatus.PartiallyRefunded;

        return refund;
    }

    /// <summary>
    /// Returns an already-recorded refund for the given idempotency key, or null if none. Used
    /// for the local idempotency check that fronts the PayPal call.
    /// </summary>
    public Refund? FindRefundByIdempotencyKey(string idempotencyKey) =>
        _refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);
}
