using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The port onto PayPal: every PayPal interaction the application needs, expressed in the application's
/// own vocabulary so ApplicationCore never references the PayPal SDK directly.
/// </summary>
public interface IPayPalPaymentGateway
{
    Task<AuthorizeResult> AuthorizeAsync(AuthorizeRequest request, CancellationToken cancellationToken);

    Task<CaptureResult> CaptureAsync(string authorizationId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Renews an authorization that has gone stale, so it can be captured again.</summary>
    Task<ReauthorizeResult> ReauthorizeAsync(string authorizationId, string idempotencyKey, CancellationToken cancellationToken);

    Task VoidAsync(string authorizationId, CancellationToken cancellationToken);

    Task<RefundResult> RefundAsync(RefundGatewayRequest request, CancellationToken cancellationToken);

    Task<SavedCardResult> SaveCardAsync(SaveCardRequest request, CancellationToken cancellationToken);

    Task DeleteSavedCardAsync(string payPalVaultId, CancellationToken cancellationToken);

    Task<ReconciliationResult> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}

public record CardDetails(string Number, string ExpiryYearMonth, string SecurityCode, string? CardholderName,
    string? AddressLine1, string? City, string? State, string? PostalCode, string? CountryCode);

public record AuthorizeRequest(
    int OrderId,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    CardDetails? Card,
    string? SavedCardVaultId);

public record AuthorizeResult(
    bool Success,
    bool RequiresBrowserApproval,
    string? PayPalOrderId,
    string? AuthorizationId,
    string? AuthorizationStatus,
    string? CardBrand,
    string? CardLast4,
    string? FailureReason);

public record CaptureResult(
    bool Success,
    bool NeedsReauthorization,
    string? CaptureId,
    string? CaptureStatus,
    decimal CapturedAmount,
    decimal? FeeAmount,
    decimal? NetAmount,
    string? FailureReason);

public record ReauthorizeResult(bool Success, string? AuthorizationId, string? AuthorizationStatus, string? FailureReason);

public record RefundGatewayRequest(string CaptureId, decimal? Amount, string Currency, string IdempotencyKey, string? Note);

public record RefundResult(bool Success, string? RefundId, string? Status, string? FailureReason);

public record SaveCardRequest(string BuyerId, string IdempotencyKey, CardDetails Card);

public record SavedCardResult(bool Success, string? PayPalVaultId, string? Brand, string? Last4, string? Expiry, string? FailureReason);

public record ReconciliationTransaction(string TransactionId, decimal Amount, string Currency, DateTimeOffset? InitiatedAt, string? InvoiceId);

public record ReconciliationResult(IReadOnlyList<ReconciliationTransaction> Transactions, bool Truncated, int PagesRead, int? TotalPages);
