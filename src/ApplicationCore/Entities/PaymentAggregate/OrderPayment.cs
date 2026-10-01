using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// Payment/fulfilment state for one <see cref="Entities.OrderAggregate.Order"/>, keyed by that
/// order's id (1:1 satellite - the Order aggregate itself stays untouched). Also doubles as the
/// duplicate-prevention claim row for authorize/capture/cancel (see OrderPaymentService).
/// </summary>
public class OrderPayment : IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private OrderPayment() { }

    /// <summary>
    /// Creates the row at the moment a shopper first pays - this insert IS the duplicate-pay claim
    /// (a racing second insert for the same OrderId is refused by the store's primary key; see
    /// OrderPaymentService.PayAsync). Before this row exists, an order's payment state is reported
    /// as <see cref="PaymentStatus.AwaitingPayment"/> without a persisted row.
    /// </summary>
    public OrderPayment(int orderId, string invoiceId, decimal amount, string currencyCode)
    {
        Guard.Against.NegativeOrZero(orderId, nameof(orderId));
        Guard.Against.NullOrEmpty(invoiceId, nameof(invoiceId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(currencyCode, nameof(currencyCode));

        OrderId = orderId;
        InvoiceId = invoiceId;
        Amount = amount;
        CurrencyCode = currencyCode;
        Status = PaymentStatus.Authorizing;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    /// <summary>Primary key - the Order this payment belongs to.</summary>
    public int OrderId { get; private set; }

    public PaymentStatus Status { get; private set; }
    public string CurrencyCode { get; private set; }
    public decimal Amount { get; private set; }
    public string InvoiceId { get; private set; }

    public string? PayPalOrderId { get; private set; }
    public string? AuthorizationId { get; private set; }
    public string? AuthorizationStatus { get; private set; }

    /// <summary>PayPal's own expiration timestamp for the current authorization, verbatim (its exact wire shape is PayPal's to define).</summary>
    public string? AuthorizationExpiryUtc { get; private set; }

    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedGross { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }

    public decimal RefundedTotal { get; private set; }
    public string? PaymentSourceDescriptor { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Optimistic-concurrency token: guards every state transition against a racing duplicate request.</summary>
    public int Version { get; private set; }

    public decimal RemainingRefundable => (CapturedGross ?? 0m) - RefundedTotal;

    /// <summary>Retries a payment after a prior authorization attempt was declined.</summary>
    public void BeginAuthorizing()
    {
        if (Status != PaymentStatus.AuthorizationFailed)
        {
            throw new PaymentAuthorizationException(
                $"Order {OrderId} cannot be paid from status {Status}.");
        }

        Status = PaymentStatus.Authorizing;
        Touch();
    }

    public void AuthorizationSucceeded(string payPalOrderId, string authorizationId, string authorizationStatus,
        string? expiryUtc, string? paymentSourceDescriptor)
    {
        if (Status != PaymentStatus.Authorizing)
        {
            throw new PaymentAuthorizationException(
                $"Order {OrderId} authorization cannot be recorded from status {Status}.");
        }

        PayPalOrderId = payPalOrderId;
        AuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        AuthorizationExpiryUtc = expiryUtc;
        PaymentSourceDescriptor = paymentSourceDescriptor;
        Status = PaymentStatus.Authorized;
        Touch();
    }

    public void AuthorizationFailed()
    {
        if (Status != PaymentStatus.Authorizing)
        {
            throw new PaymentAuthorizationException(
                $"Order {OrderId} authorization failure cannot be recorded from status {Status}.");
        }

        Status = PaymentStatus.AuthorizationFailed;
        Touch();
    }

    public void BeginFulfilling()
    {
        if (Status != PaymentStatus.Authorized)
        {
            throw new PaymentAuthorizationException(
                $"Order {OrderId} cannot be fulfilled from status {Status}.");
        }

        Status = PaymentStatus.Fulfilling;
        Touch();
    }

    /// <summary>Records a renewed authorization obtained mid-fulfilment because the original had gone stale.</summary>
    public void RenewAuthorization(string newAuthorizationId, string authorizationStatus, string? expiryUtc)
    {
        if (Status != PaymentStatus.Fulfilling)
        {
            throw new PaymentAuthorizationException(
                $"Order {OrderId} authorization cannot be renewed from status {Status}.");
        }

        AuthorizationId = newAuthorizationId;
        AuthorizationStatus = authorizationStatus;
        AuthorizationExpiryUtc = expiryUtc;
        Touch();
    }

    public void FulfillmentFailed()
    {
        if (Status != PaymentStatus.Fulfilling)
        {
            throw new PaymentAuthorizationException(
                $"Order {OrderId} fulfilment failure cannot be recorded from status {Status}.");
        }

        // Revert to Authorized so fulfilment can be retried.
        Status = PaymentStatus.Authorized;
        Touch();
    }

    public void CaptureSucceeded(string captureId, string captureStatus, decimal capturedGross, decimal? fee, decimal? net)
    {
        if (Status != PaymentStatus.Fulfilling)
        {
            throw new PaymentAuthorizationException(
                $"Order {OrderId} capture cannot be recorded from status {Status}.");
        }

        CaptureId = captureId;
        CaptureStatus = captureStatus;
        CapturedGross = capturedGross;
        PayPalFee = fee;
        NetAmount = net;
        Status = PaymentStatus.Captured;
        Touch();
    }

    public void Cancel()
    {
        if (Status != PaymentStatus.Authorized)
        {
            throw new PaymentAuthorizationException(
                $"Order {OrderId} cannot be cancelled from status {Status}.");
        }

        Status = PaymentStatus.Cancelled;
        Touch();
    }

    public void RecordRefund(decimal amount)
    {
        if (Status != PaymentStatus.Captured && Status != PaymentStatus.PartiallyRefunded)
        {
            throw new PaymentAuthorizationException(
                $"Order {OrderId} cannot be refunded from status {Status}.");
        }

        if (amount <= 0 || amount > RemainingRefundable + 0.005m)
        {
            throw new RefundAmountExceededException(OrderId, amount, RemainingRefundable);
        }

        RefundedTotal += amount;
        Status = RefundedTotal >= (CapturedGross ?? 0m) - 0.005m
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;
        Touch();
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Version++;
    }
}
