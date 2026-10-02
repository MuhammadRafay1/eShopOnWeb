using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>A catalog item and quantity in a placed order.</summary>
public record OrderLineInput(int CatalogItemId, int Quantity);

/// <summary>Where to ship an order. Optional on placement; a placeholder is used when omitted.</summary>
public record ShippingAddressInput(string? Street, string? City, string? State, string? Country, string? ZipCode);

/// <summary>How to pay an order: a one-off card, or one of the shopper's saved cards.</summary>
public record PayInput
{
    public CardInput? Card { get; init; }
    public int? SavedPaymentMethodId { get; init; }
}

/// <summary>A refund request: an optional partial amount, plus a caller-supplied idempotency key.</summary>
public record RefundInput
{
    public decimal? Amount { get; init; }
    public required string IdempotencyKey { get; init; }
}

/// <summary>A refund's state, for responses.</summary>
public record RefundLineView(int RefundId, string? PayPalRefundId, decimal Amount, string Status, DateTimeOffset CreatedAt);

/// <summary>A projection of an order's payment state for API responses and my-orders.</summary>
public record OrderPaymentView
{
    public required int OrderId { get; init; }
    public required string BuyerId { get; init; }
    public required string PaymentStatus { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal Amount { get; init; }
    public DateTimeOffset OrderDate { get; init; }
    public string InvoiceId { get; init; } = string.Empty;

    public string? PayPalOrderId { get; init; }
    public string? AuthorizationId { get; init; }
    public string? AuthorizationStatus { get; init; }
    public DateTimeOffset? AuthorizationExpiresAt { get; init; }

    public string? CaptureId { get; init; }
    public string? CaptureStatus { get; init; }
    public decimal? CapturedAmount { get; init; }
    public decimal? PayPalFee { get; init; }
    public decimal? NetAmount { get; init; }
    public decimal TotalRefunded { get; init; }

    public IReadOnlyList<RefundLineView> Refunds { get; init; } = Array.Empty<RefundLineView>();
    public IReadOnlyList<OrderLineView> Items { get; init; } = Array.Empty<OrderLineView>();

    public static OrderPaymentView From(OrderPayment p, DateTimeOffset orderDate, IReadOnlyList<OrderLineView> items)
    {
        var refunds = new List<RefundLineView>();
        foreach (var r in p.Refunds)
        {
            refunds.Add(new RefundLineView(r.Id, r.PayPalRefundId, r.Amount, r.Status, r.CreatedAt));
        }

        return new OrderPaymentView
        {
            OrderId = p.OrderId,
            BuyerId = p.BuyerId,
            PaymentStatus = p.Status.ToString(),
            CurrencyCode = p.CurrencyCode,
            Amount = p.Amount,
            OrderDate = orderDate,
            InvoiceId = p.InvoiceId,
            PayPalOrderId = p.PayPalOrderId,
            AuthorizationId = p.AuthorizationId,
            AuthorizationStatus = p.AuthorizationStatus,
            AuthorizationExpiresAt = p.AuthorizationExpiresAt,
            CaptureId = p.CaptureId,
            CaptureStatus = p.CaptureStatus,
            CapturedAmount = p.CapturedAmount,
            PayPalFee = p.PayPalFee,
            NetAmount = p.NetAmount,
            TotalRefunded = p.TotalRefunded(),
            Refunds = refunds,
            Items = items
        };
    }
}

/// <summary>A line item on an order, for my-orders responses.</summary>
public record OrderLineView(int CatalogItemId, string ProductName, decimal UnitPrice, int Units);

/// <summary>A saved card's safe description, for responses.</summary>
public record SavedCardView(int PaymentMethodId, string? Brand, string? LastFourDigits, string? Expiry, string? CardholderName, DateTimeOffset CreatedAt);

/// <summary>How a PayPal transaction lines up against eShop's records.</summary>
public enum ReconciliationMatch
{
    /// <summary>Present in both PayPal and eShop.</summary>
    Matched = 0,
    /// <summary>PayPal knows about it, eShop does not.</summary>
    PayPalOnly = 1,
    /// <summary>eShop captured it, PayPal's report does not (yet) list it.</summary>
    EShopOnly = 2
}

/// <summary>One line of the reconciliation report.</summary>
public record ReconciliationLine
{
    public required ReconciliationMatch Match { get; init; }
    public string? PayPalTransactionId { get; init; }
    public string? PayPalStatus { get; init; }
    public decimal? PayPalAmount { get; init; }
    public decimal? PayPalFee { get; init; }
    public string? CurrencyCode { get; init; }
    public string? InvoiceId { get; init; }
    public string? EventCode { get; init; }
    public DateTimeOffset? TransactionDate { get; init; }

    public int? OrderId { get; init; }
    public string? EShopPaymentStatus { get; init; }
    public decimal? EShopAmount { get; init; }
}

/// <summary>The reconciliation report for a date range.</summary>
public record ReconciliationReport
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }

    /// <summary>True when every page of every sub-window was walked (never truncated).</summary>
    public required bool Complete { get; init; }

    public int PayPalTransactionCount { get; init; }
    public int MatchedCount { get; init; }
    public int PayPalOnlyCount { get; init; }
    public int EShopOnlyCount { get; init; }

    public IReadOnlyList<ReconciliationLine> Lines { get; init; } = Array.Empty<ReconciliationLine>();
}
