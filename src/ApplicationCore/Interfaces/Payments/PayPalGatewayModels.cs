using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

/// <summary>
/// A request to authorize an order total. Exactly one of <see cref="Card"/> or
/// <see cref="VaultId"/> is populated (validated at the endpoint layer before calling).
/// </summary>
public class PayPalAuthorizeRequest
{
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }

    /// <summary>The eShop order id as a string - set as PayPal's invoice_id (reconciliation join key).</summary>
    public required string InvoiceId { get; init; }

    /// <summary>Value for the PayPal-Request-Id idempotency header.</summary>
    public required string IdempotencyKey { get; init; }

    public CardDetails? Card { get; init; }
    public string? VaultId { get; init; }
}

public class PayPalAuthorizationOutcome
{
    public required bool Approved { get; init; }
    public required string PayPalOrderId { get; init; }
    public string? AuthorizationId { get; init; }
    public PaymentAuthorizationStatus Status { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>PayPal's decline reason when <see cref="Approved"/> is false.</summary>
    public string? DeclineReason { get; init; }
}

public enum PayPalCaptureResult { Completed, AuthorizationExpired, Failed }

public class PayPalCaptureOutcome
{
    public required PayPalCaptureResult Result { get; init; }
    public string? CaptureId { get; init; }
    public PaymentCaptureStatus? Status { get; init; }
    public decimal GrossAmount { get; init; }
    public decimal FeeAmount { get; init; }
    public decimal NetAmount { get; init; }

    /// <summary>PayPal's own issue code / description when the capture did not complete.</summary>
    public string? FailureIssue { get; init; }
    public string? FailureDescription { get; init; }
}

public enum PayPalReauthorizeResult { Renewed, Failed }

public class PayPalReauthorizeOutcome
{
    public required PayPalReauthorizeResult Result { get; init; }
    public PaymentAuthorizationStatus Status { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public string? FailureIssue { get; init; }
    public string? FailureDescription { get; init; }
}

public class PayPalRefundOutcome
{
    public required string RefundId { get; init; }

    /// <summary>Mirrors PayPal's refund_status string (COMPLETED, PENDING, ...).</summary>
    public required string Status { get; init; }
}

/// <summary>A card as PayPal describes it back after vaulting - safe descriptor, never the PAN.</summary>
public class PayPalSavedCard
{
    public required string VaultId { get; init; }
    public required string Last4 { get; init; }
    public required string Brand { get; init; }
    public required string ExpiryYearMonth { get; init; }
    public string? CardholderName { get; init; }
}

/// <summary>PayPal's own record of one money-movement event, for reconciliation.</summary>
public class PayPalTransactionRecord
{
    public required string TransactionId { get; init; }
    public string? InvoiceId { get; init; }
    public decimal Amount { get; init; }
    public string? CurrencyCode { get; init; }
    public string? Status { get; init; }
    public DateTimeOffset InitiationDate { get; init; }
    public decimal? FeeAmount { get; init; }
}
