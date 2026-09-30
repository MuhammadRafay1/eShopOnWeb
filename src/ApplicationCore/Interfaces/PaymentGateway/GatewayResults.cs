using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;

/// <summary>
/// Result of an authorize/reauthorize call. <see cref="StatusRaw"/> carries PayPal's status string verbatim
/// (ApplicationCore takes no dependency on the SDK enum types). <see cref="RequiresPayerAction"/> is set when
/// PayPal wants a browser challenge (3DS) — the orchestration layer must stop rather than build a redirect.
/// </summary>
public record AuthorizationResult(
    string PayPalOrderId,
    string PayPalAuthorizationId,
    string StatusRaw,
    decimal Amount,
    DateTimeOffset ExpiresAt,
    bool RequiresPayerAction);

/// <summary>Result of capturing an authorization, including PayPal's fee and the net proceeds to the merchant.</summary>
public record CaptureResult(
    string PayPalCaptureId,
    string StatusRaw,
    decimal CapturedAmount,
    decimal? PayPalFeeAmount,
    decimal? NetAmount);

/// <summary>Result of refunding a capture. <see cref="TotalRefundedOnCapture"/> is PayPal's cumulative figure.</summary>
public record RefundResult(
    string PayPalRefundId,
    string StatusRaw,
    decimal Amount,
    decimal? TotalRefundedOnCapture);

/// <summary>Result of vaulting a card — the vault id plus safe-to-display metadata (never a PAN).</summary>
public record VaultedCardResult(
    string VaultId,
    string? Brand,
    string? LastFour,
    string? Expiry);

/// <summary>One transaction PayPal reports in a reconciliation search window.</summary>
public record PayPalTransactionRecord(
    string TransactionId,
    decimal? Amount,
    string? CurrencyCode,
    string? StatusRaw,
    DateTimeOffset? InitiatedAt,
    string? InvoiceId,
    string? CustomField);
