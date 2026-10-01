using System;

namespace Microsoft.eShopWeb.ApplicationCore.Payments;

/// <summary>Result of authorizing (holding) an order total at PayPal.</summary>
public sealed record AuthorizationResult(
    string PayPalOrderId,
    string AuthorizationId,
    string AuthorizationStatus,
    DateTimeOffset? ExpiresAt);

/// <summary>Result of capturing (taking) an authorized payment, with PayPal's reported fee/net breakdown.</summary>
public sealed record CaptureResult(
    string CaptureId,
    string CaptureStatus,
    decimal CapturedAmount,
    decimal? FeeAmount,
    decimal? NetAmount);

/// <summary>Result of renewing a stale authorization.</summary>
public sealed record ReauthorizationResult(
    string AuthorizationId,
    string AuthorizationStatus,
    DateTimeOffset? ExpiresAt);

/// <summary>Result of refunding a captured payment, in full or in part.</summary>
public sealed record RefundResult(
    string RefundId,
    string Status,
    decimal Amount);

/// <summary>Result of saving a card to the vault. Describes the card safely — never full card details.</summary>
public sealed record SavedCardResult(
    string PaymentTokenId,
    string? Last4,
    string? Brand,
    string? Expiry);

/// <summary>A re-read of a PayPal order used to settle an unknown write outcome.</summary>
public sealed record OrderSnapshot(
    string PayPalOrderId,
    string? OrderStatus,
    string? AuthorizationId,
    string? AuthorizationStatus,
    DateTimeOffset? AuthorizationExpiresAt,
    string? CaptureId,
    string? CaptureStatus,
    decimal? CapturedAmount,
    decimal? FeeAmount,
    decimal? NetAmount);
