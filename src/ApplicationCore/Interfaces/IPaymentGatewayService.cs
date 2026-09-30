using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Port over the payment processor. Every parameter and return type is a primitive or a record
/// defined here — never a PayPal SDK type — so ApplicationCore takes no compile-time dependency on
/// the SDK. The Infrastructure implementation is the only place SDK types are referenced.
/// </summary>
public interface IPaymentGatewayService
{
    Task<AuthorizationResult> AuthorizeAsync(AuthorizationRequest request, CancellationToken ct = default);
    Task<AuthorizationSnapshot> GetAuthorizationStatusAsync(string authorizationId, CancellationToken ct = default);
    Task<AuthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string orderIdForIdempotency, CancellationToken ct = default);
    Task VoidAuthorizationAsync(string authorizationId, string orderIdForIdempotency, CancellationToken ct = default);
    Task<CaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency, string orderIdForIdempotency, CancellationToken ct = default);
    Task<RefundResult> RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey, CancellationToken ct = default);
    Task<VaultedCardResult> VaultCardAsync(CardDetails card, CancellationToken ct = default);
    Task DeleteVaultedCardAsync(string vaultId, CancellationToken ct = default);
    Task<IReadOnlyList<ReconciliationTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

public record CardDetails(string Name, string Number, string Expiry, string SecurityCode, BillingAddress? BillingAddress);

public record BillingAddress(string? Line1, string? Line2, string? City, string? State, string? PostalCode, string CountryCode);

/// <summary>Either <see cref="Card"/> or <see cref="VaultId"/> is set (never both).</summary>
public record AuthorizationRequest(decimal Amount, string Currency, CardDetails? Card, string? VaultId, string IdempotencyKeyBase);

/// <summary>
/// <see cref="PayerActionRequired"/> is a first-class, expected outcome (3DS challenge), not a failure;
/// when true the other fields are null and no Payment should be recorded.
/// </summary>
public record AuthorizationResult(bool PayerActionRequired, string? PayPalOrderId, string? AuthorizationId, string? Status, DateTimeOffset? ExpiresAt);

public record AuthorizationSnapshot(string Status, DateTimeOffset? ExpiresAt);

public record CaptureResult(string CaptureId, string Status, decimal Amount, decimal? PayPalFee, decimal? NetAmount, string Currency);

public record RefundResult(string RefundId, string Status, decimal Amount);

public record VaultedCardResult(string VaultId, string Brand, string LastDigits, string Expiry);

public record ReconciliationTransaction(string TransactionId, decimal Amount, string Currency, string Status, DateTimeOffset InitiatedAt);
