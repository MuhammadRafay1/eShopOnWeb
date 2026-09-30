using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the additive PayPal payment flows on top of the existing order model.
/// Endpoints stay thin: they extract the caller identity from the token, call one method
/// here, and map the result / thrown domain exception to an HTTP response. All PayPal
/// interaction goes through <see cref="IPayPalClient"/>.
/// </summary>
public interface IPaymentService
{
    Task<PlaceOrderResult> PlaceOrderAsync(string buyerId, IReadOnlyList<PlaceOrderLine> lines, PaymentAddressInput shipTo, CancellationToken ct);

    /// <param name="buyerId">Caller; must own the order.</param>
    Task<PayResult> PayOrderAsync(string buyerId, int orderId, CardInput? card, int? paymentMethodId, CancellationToken ct);

    /// <summary>Operator action — no ownership check (already role-gated at the endpoint).</summary>
    Task<FulfilResult> FulfilOrderAsync(int orderId, CancellationToken ct);

    /// <summary>Operator action.</summary>
    Task<CancelResult> CancelOrderAsync(int orderId, CancellationToken ct);

    /// <param name="buyerId">Caller; must own the order (refunds are shopper-scoped per the task).</param>
    Task<OrderRefundResult> RefundOrderAsync(string buyerId, int orderId, decimal? amount, string idempotencyKey, CancellationToken ct);

    Task<IReadOnlyList<OrderSummary>> GetOrdersForBuyerAsync(string buyerId, CancellationToken ct);

    Task<SavedCardResult> SaveCardAsync(string buyerId, CardInput card, CancellationToken ct);

    Task<IReadOnlyList<SavedCardResult>> ListCardsAsync(string buyerId, CancellationToken ct);

    Task DeleteCardAsync(string buyerId, int paymentMethodId, CancellationToken ct);

    /// <summary>Operator action.</summary>
    Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}

// ---- inputs ----

public record PlaceOrderLine(int CatalogItemId, int Quantity);

public record PaymentAddressInput(string? Street, string? City, string? State, string? Country, string? ZipCode);

/// <summary>Raw card fields as received from the shopper. Never persisted; used only to build the PayPal call.</summary>
public record CardInput(
    string Number,
    int ExpiryMonth,
    int ExpiryYear,
    string? SecurityCode,
    string? CardholderName,
    PaymentAddressInput? BillingAddress);

// ---- results ----

public record PlaceOrderResult(int OrderId, decimal Total, string Currency);

public record PayResult(
    int OrderId,
    string OrderStatus,
    string AuthorizationId,
    string AuthorizationStatus,
    DateTimeOffset? AuthorizationExpiresAt,
    decimal Amount,
    string Currency,
    bool AlreadyAuthorized);

public record FulfilResult(
    int OrderId,
    string OrderStatus,
    string CaptureId,
    string CaptureStatus,
    decimal CapturedAmount,
    decimal? PayPalFee,
    decimal? NetAmount,
    string Currency,
    bool Reauthorized,
    bool AlreadyFulfilled);

public record CancelResult(int OrderId, string OrderStatus, bool AuthorizationVoided, bool AlreadyCancelled);

public record OrderRefundResult(
    string RefundId,
    string Status,
    decimal Amount,
    decimal TotalRefunded,
    string OrderStatus,
    bool AlreadyProcessed);

public record RefundSummary(string RefundId, decimal Amount, string Status, DateTimeOffset CreatedAt);

public record OrderSummary(
    int OrderId,
    DateTimeOffset OrderDate,
    string OrderStatus,
    decimal Total,
    string Currency,
    PaymentSummary? Payment);

public record PaymentSummary(
    string? AuthorizationId,
    string? AuthorizationStatus,
    DateTimeOffset? AuthorizationExpiresAt,
    string? CaptureId,
    string? CaptureStatus,
    decimal? CapturedAmount,
    decimal? PayPalFee,
    decimal? NetAmount,
    decimal TotalRefunded,
    IReadOnlyList<RefundSummary> Refunds);

public record SavedCardResult(int PaymentMethodId, string? Brand, string? LastDigits, string? Expiry, DateTimeOffset CreatedAt);

// ---- reconciliation ----

public record ReconciliationReport(
    DateTimeOffset From,
    DateTimeOffset To,
    int PayPalTransactionsScanned,
    string EShopTimestampBasis,
    IReadOnlyList<MatchedReconItem> Matched,
    IReadOnlyList<PayPalOnlyReconItem> PayPalOnly,
    IReadOnlyList<EShopOnlyReconItem> EShopOnly);

public record MatchedReconItem(int OrderId, string InvoiceId, string PayPalTransactionId, decimal EShopAmount, decimal PayPalAmount, string? PayPalStatus);

public record PayPalOnlyReconItem(string PayPalTransactionId, string? InvoiceId, decimal Amount, string? Status, DateTimeOffset? Date);

public record EShopOnlyReconItem(int OrderId, string InvoiceId, decimal CapturedAmount, DateTimeOffset? CapturedAt);
