using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

public enum PaymentStatus
{
    /// <summary>An authorization attempt is under way (or its outcome is still being settled).</summary>
    Pending = 0,
    Authorized = 1,
    Declined = 2,
    /// <summary>The provider wants the shopper to complete a challenge (e.g. 3-D Secure) in a browser.</summary>
    ActionRequired = 3,
    AuthorizationExpired = 4,
    Voided = 5,
    Captured = 6,
    PartiallyRefunded = 7,
    Refunded = 8
}

/// <summary>
/// The payment that settles an <see cref="Order"/>: the provider-side identifiers and statuses for the hold
/// (authorization), the capture and the refunds, so that any later request can act on it.
/// Never holds card numbers or security codes — only the brand and last digits the provider reports back.
/// </summary>
public class OrderPayment : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private OrderPayment() { }

    internal OrderPayment(string provider, string currency, decimal amount)
    {
        Provider = provider;
        Currency = currency;
        Amount = amount;
        Status = PaymentStatus.Pending;
    }

    public int OrderId { get; private set; }
    public string Provider { get; private set; }
    public string Currency { get; private set; }
    /// <summary>The amount the shopper is asked to pay (the order total at the time of payment).</summary>
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }

    public int? SavedPaymentMethodId { get; private set; }
    public string? CardBrand { get; private set; }
    public string? CardLastDigits { get; private set; }

    public string? ProviderOrderId { get; private set; }
    public string? ProviderOrderStatus { get; private set; }

    public string? AuthorizationId { get; private set; }
    public string? AuthorizationStatus { get; private set; }
    public decimal? AuthorizedAmount { get; private set; }
    public DateTimeOffset? AuthorizedAt { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }
    public int ReauthorizationCount { get; private set; }

    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? ProviderFee { get; private set; }
    public decimal? NetAmount { get; private set; }
    public DateTimeOffset? CapturedAt { get; private set; }

    public DateTimeOffset? VoidedAt { get; private set; }

    /// <summary>Last failure, phrased for an operator or shopper to act on.</summary>
    public string? LastFailure { get; private set; }

    private readonly List<PaymentRefund> _refunds = new();
    public IReadOnlyCollection<PaymentRefund> Refunds => _refunds.AsReadOnly();

    /// <summary>Refunds that hold (or may hold) money: everything except failed or cancelled ones.</summary>
    public decimal RefundedOrReservedAmount =>
        _refunds.Where(r => r.State is not (RefundState.Failed or RefundState.Cancelled)).Sum(r => r.Amount);

    public decimal RefundedAmount => _refunds.Where(r => r.State == RefundState.Completed).Sum(r => r.Amount);

    public decimal RefundableAmount => Math.Max(0m, (CapturedAmount ?? 0m) - RefundedOrReservedAmount);

    internal void StartAttempt(string currency, decimal amount, int? savedPaymentMethodId)
    {
        Currency = currency;
        Amount = amount;
        SavedPaymentMethodId = savedPaymentMethodId;
        Status = PaymentStatus.Pending;
        ProviderOrderId = null;
        ProviderOrderStatus = null;
        AuthorizationId = null;
        AuthorizationStatus = null;
        AuthorizedAmount = null;
        AuthorizedAt = null;
        AuthorizationExpiresAt = null;
        CardBrand = null;
        CardLastDigits = null;
        LastFailure = null;
    }

    internal void RecordProviderOrder(string providerOrderId, string? providerOrderStatus)
    {
        ProviderOrderId = providerOrderId;
        ProviderOrderStatus = providerOrderStatus;
    }

    internal void RecordCard(string? brand, string? lastDigits)
    {
        CardBrand = brand ?? CardBrand;
        CardLastDigits = lastDigits ?? CardLastDigits;
    }

    internal void RecordAuthorization(string authorizationId, string? status, decimal? amount,
        DateTimeOffset? authorizedAt, DateTimeOffset? expiresAt)
    {
        AuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizedAmount = amount;
        AuthorizedAt = authorizedAt;
        AuthorizationExpiresAt = expiresAt;
        Status = PaymentStatus.Authorized;
        LastFailure = null;
    }

    internal void RecordReauthorization(string authorizationId, string? status, decimal? amount,
        DateTimeOffset? authorizedAt, DateTimeOffset? expiresAt)
    {
        RecordAuthorization(authorizationId, status, amount, authorizedAt, expiresAt ?? AuthorizationExpiresAt);
        ReauthorizationCount++;
    }

    internal void RecordAuthorizationStatus(string? status) => AuthorizationStatus = status ?? AuthorizationStatus;

    internal void RecordFailure(PaymentStatus status, string reason)
    {
        Status = status;
        LastFailure = reason;
    }

    internal void RecordNote(string reason) => LastFailure = reason;

    internal void RecordCapture(string captureId, string? status, decimal? amount, decimal? fee, decimal? net,
        DateTimeOffset? capturedAt)
    {
        CaptureId = captureId;
        CaptureStatus = status;
        CapturedAmount = amount;
        ProviderFee = fee;
        NetAmount = net;
        CapturedAt = capturedAt;
        Status = PaymentStatus.Captured;
        LastFailure = null;
    }

    internal void RecordVoid(string? authorizationStatus, DateTimeOffset voidedAt)
    {
        AuthorizationStatus = authorizationStatus ?? AuthorizationStatus;
        VoidedAt = voidedAt;
        Status = PaymentStatus.Voided;
    }

    internal PaymentRefund AddRefund(string idempotencyKey, decimal amount, DateTimeOffset requestedAt)
    {
        var refund = new PaymentRefund(idempotencyKey, amount, Currency, requestedAt);
        _refunds.Add(refund);
        return refund;
    }

    internal void RecalculateRefundStatus()
    {
        if (CapturedAmount is null) return;
        var refunded = RefundedAmount;
        Status = refunded <= 0m
            ? PaymentStatus.Captured
            : refunded >= CapturedAmount.Value ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
    }
}
