using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// A one-off card supplied directly on a pay/save-card request. Transient - never persisted by
/// this application; it only ever travels through to PayPal.
/// </summary>
public record PayPalCardDetails(
    string Number,
    string ExpiryYearMonth, // "YYYY-MM"
    string CardholderName,
    string AddressLine1,
    string City,
    string State,
    string PostalCode,
    string CountryCode);

public record PayPalAuthorizationResult(
    string PayPalOrderId,
    string AuthorizationId,
    string AuthorizationStatus,
    string? CardBrand,
    string? CardLast4);

public record PayPalCaptureResult(
    string CaptureId,
    string CaptureStatus,
    decimal GrossAmount,
    decimal PayPalFee,
    decimal NetAmount,
    DateTimeOffset CaptureTime);

public record PayPalReauthorizationResult(string AuthorizationId, string Status);

public record PayPalRefundResult(string RefundId, string Status);

public record PayPalSetupTokenResult(string SetupTokenId, string CustomerId, string? CardBrand, string? CardLast4, string? Expiry);

public record PayPalPaymentTokenResult(string VaultId, string CustomerId, string? CardBrand, string? CardLast4, string? Expiry);

public record PayPalTransactionRecord(
    string TransactionId,
    string? ReferenceId,
    string Status,
    decimal Amount,
    decimal? FeeAmount,
    string CurrencyCode,
    DateTimeOffset InitiatedDate,
    string? InvoiceId,
    string? CustomField);

public record PayPalTransactionPage(IReadOnlyList<PayPalTransactionRecord> Transactions, int Page, int TotalPages);

/// <summary>
/// Thin, typed wrapper over the PayPal REST calls this integration needs. Never leaks HttpClient
/// or raw PayPal JSON to callers - only small result records carrying the ids/statuses/amounts
/// the domain needs to remember.
/// </summary>
public interface IPayPalClient
{
    Task<PayPalAuthorizationResult> AuthorizeOrderWithCardAsync(decimal amount, string currency, string customId,
        string invoiceId, PayPalCardDetails card, string idempotencyKey, CancellationToken ct = default);

    Task<PayPalAuthorizationResult> AuthorizeOrderWithVaultAsync(decimal amount, string currency, string customId,
        string invoiceId, string vaultId, string idempotencyKey, CancellationToken ct = default);

    Task<PayPalCaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency,
        string invoiceId, string idempotencyKey, CancellationToken ct = default);

    Task<PayPalReauthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency,
        CancellationToken ct = default);

    Task VoidAsync(string authorizationId, CancellationToken ct = default);

    Task<PayPalRefundResult> RefundAsync(string captureId, decimal amount, string currency, string customId,
        string invoiceId, string idempotencyKey, CancellationToken ct = default);

    Task<PayPalSetupTokenResult> CreateSetupTokenAsync(PayPalCardDetails card, string idempotencyKey,
        CancellationToken ct = default);

    Task<PayPalPaymentTokenResult> CreatePaymentTokenAsync(string setupTokenId, string idempotencyKey,
        CancellationToken ct = default);

    Task DeletePaymentTokenAsync(string vaultId, CancellationToken ct = default);

    Task<PayPalTransactionPage> ListTransactionsAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, int page,
        int pageSize, CancellationToken ct = default);
}
