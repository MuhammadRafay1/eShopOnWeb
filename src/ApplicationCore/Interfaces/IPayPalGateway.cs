using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Transient input for a one-off card payment or a card save. Never persisted, never logged.
/// </summary>
public record CardDetails(
    string Number,
    string Expiry,
    string SecurityCode,
    string Name,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? State,
    string PostalCode,
    string CountryCode);

public record AuthorizeResult(
    string PayPalOrderId,
    string AuthorizationId,
    string AuthorizationStatus,
    DateTimeOffset? ExpiresAt,
    string? CardBrand,
    string? CardLast4);

public record CaptureResult(
    string CaptureId,
    string Status,
    decimal GrossAmount,
    decimal? PayPalFee,
    decimal? NetAmount);

public record AuthorizationInfo(
    string Status,
    DateTimeOffset? ExpiresAt);

public record RefundResult(
    string RefundId,
    string Status,
    decimal? TotalRefunded);

public record SavedCardInfo(
    string VaultId,
    string? CustomerId,
    string? Brand,
    string? Last4,
    string? Expiry,
    string? CardType);

public record PayPalTransaction(
    string TransactionId,
    string Status,
    decimal Amount,
    decimal? FeeAmount,
    string? InvoiceId,
    DateTimeOffset? InitiatedDate);

/// <summary>
/// Domain-facing gateway to PayPal. ApplicationCore depends only on this interface and the
/// plain result types above; all HTTP/JSON/spec-shaped detail lives behind the Infrastructure
/// implementation so the application layer never sees a PayPal DTO.
/// </summary>
public interface IPayPalGateway
{
    Task<AuthorizeResult> AuthorizeAsync(decimal amount, string currency, CardDetails? card, string? vaultId, string invoiceId, string requestId, CancellationToken ct = default);

    Task<CaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency, string invoiceId, string requestId, CancellationToken ct = default);

    Task<AuthorizeResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string requestId, CancellationToken ct = default);

    Task VoidAsync(string authorizationId, string requestId, CancellationToken ct = default);

    Task<AuthorizationInfo> GetAuthorizationAsync(string authorizationId, CancellationToken ct = default);

    Task<RefundResult> RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey, CancellationToken ct = default);

    Task<SavedCardInfo> SaveCardAsync(CardDetails card, string? customerId, string requestId, CancellationToken ct = default);

    Task DeleteCardAsync(string vaultId, CancellationToken ct = default);

    Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
