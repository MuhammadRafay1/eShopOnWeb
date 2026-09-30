using System;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

public record BillingAddress(
    string CountryCode,
    string? AddressLine1 = null,
    string? AdminArea1 = null,
    string? AdminArea2 = null,
    string? PostalCode = null);

/// <summary>
/// Raw, one-off card details for a card the shopper is not saving. Lives only for the
/// duration of a single request to PayPal - never persisted, never logged.
/// </summary>
public record CardDetails(
    string Number,
    int ExpiryMonth,
    int ExpiryYear,
    string SecurityCode,
    string Name,
    BillingAddress BillingAddress)
{
    /// <summary>PayPal's Internet-date card expiry format, e.g. "2028-04".</summary>
    public string ToPayPalExpiry() => $"{ExpiryYear:D4}-{ExpiryMonth:D2}";
}

public class PayPalAuthorizeRequest
{
    public required int OrderId { get; init; }
    public required string InvoiceId { get; init; }
    public required decimal Amount { get; init; }
    public CardDetails? Card { get; init; }
    public string? VaultId { get; init; }
}

public record PayPalAuthorizationResult(
    string PaypalOrderId,
    string AuthorizationId,
    string Status,
    DateTimeOffset? ExpiresAt,
    decimal Amount);

public record PayPalCaptureResult(
    string CaptureId,
    string Status,
    decimal CapturedAmount,
    decimal? Fee,
    decimal? NetAmount);

public class PayPalRefundRequest
{
    public required string CaptureId { get; init; }
    /// <summary>Null means a full refund of the remaining captured amount.</summary>
    public decimal? Amount { get; init; }
    public required string OrderId { get; init; }
    public string? InvoiceId { get; init; }
    public required string IdempotencyKey { get; init; }
}

public record PayPalRefundResult(
    string RefundId,
    string Status,
    decimal Amount,
    decimal TotalRefunded);

public record PayPalVaultResult(
    string VaultTokenId,
    string? CustomerId,
    string? Last4,
    string? Brand,
    string? Expiry);

public record PayPalTransactionRecord(
    string TransactionId,
    DateTimeOffset InitiatedAt,
    decimal Amount,
    decimal? Fee,
    string Status,
    string? CustomField,
    string? InvoiceId);
