using System;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

public record AuthorizationResult(
    string PayPalOrderId,
    string AuthorizationId,
    string Status,
    DateTimeOffset? ExpiresAt,
    string? CardBrand,
    string? CardLast4,
    bool RequiresChallenge,
    string? ChallengeReason);

public record CaptureResult(
    string CaptureId,
    string Status,
    decimal GrossAmount,
    decimal? PayPalFee,
    decimal? NetAmount);

public record ReauthorizeResult(
    string AuthorizationId,
    string Status,
    DateTimeOffset? ExpiresAt);

public record RefundResult(
    string RefundId,
    string Status,
    decimal Amount,
    decimal TotalRefunded);

public record VaultResult(
    string VaultTokenId,
    string? CustomerId,
    string? Brand,
    string? Last4,
    string? Expiry);

public record AuthorizationStatusResult(
    string Status,
    DateTimeOffset? ExpiresAt);

public record PayPalTransaction(
    string TransactionId,
    string? EventCode,
    string? Status,
    decimal Amount,
    string CurrencyCode,
    decimal? FeeAmount,
    string? InvoiceId,
    string? CustomField,
    DateTimeOffset? InitiatedDate);
