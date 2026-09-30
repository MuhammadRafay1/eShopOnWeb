using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Request to authorize an order's payment: exactly one of Card / PaymentMethodId is set by
/// the caller (the endpoint validates this before calling the service).
/// </summary>
public class PaymentAuthorizeInput
{
    public PayPalCardInput? Card { get; init; }
    public bool SaveCard { get; init; }
    public int? PaymentMethodId { get; init; }
}

public class OrderLineInput
{
    public required int CatalogItemId { get; init; }
    public required int Quantity { get; init; }
}

/// <summary>Result of a refund request: the refund itself plus the resulting order/payment state.</summary>
public class PaymentRefundResult
{
    public required string RefundId { get; init; }
    public required string Status { get; init; }
    public required decimal Amount { get; init; }
    public required string OrderPaymentStatus { get; init; }
    public required decimal TotalRefunded { get; init; }
    public required decimal RefundableRemaining { get; init; }
}

public class ReconciliationReport
{
    public DateTimeOffset From { get; init; }
    public DateTimeOffset To { get; init; }
    public IReadOnlyList<ReconciliationMatch> Matched { get; init; } = Array.Empty<ReconciliationMatch>();
    public IReadOnlyList<PayPalTransaction> PayPalOnly { get; init; } = Array.Empty<PayPalTransaction>();
    public IReadOnlyList<ReconciliationEShopOnlyRow> EShopOnly { get; init; } = Array.Empty<ReconciliationEShopOnlyRow>();
}

public class ReconciliationMatch
{
    public required int OrderId { get; init; }
    public required string TransactionId { get; init; }
    public decimal EShopCapturedAmount { get; init; }
    public decimal? PayPalAmount { get; init; }
    public decimal? PayPalFeeAmount { get; init; }
    public string? PayPalStatus { get; init; }
}

public class ReconciliationEShopOnlyRow
{
    public required int OrderId { get; init; }
    public required string CaptureId { get; init; }
    public decimal CapturedAmount { get; init; }
}
