using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.PublicApi.PaymentModels;

/// <summary>Safe, read-only view of an order together with its payment state.</summary>
public class OrderView
{
    public int OrderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset OrderDate { get; init; }
    public decimal Total { get; init; }
    public string Currency { get; init; } = string.Empty;
    public PaymentView? Payment { get; init; }

    public static OrderView From(Order order, Payment? payment, string currency) => new()
    {
        OrderId = order.Id,
        Status = order.Status.ToString(),
        OrderDate = order.OrderDate,
        Total = order.Total(),
        Currency = payment?.CurrencyCode ?? currency,
        Payment = payment is null ? null : PaymentView.From(payment)
    };
}

/// <summary>Safe view of a payment's PayPal-owned state.</summary>
public class PaymentView
{
    public string AuthorizationStatus { get; init; } = string.Empty;
    public decimal AuthorizedAmount { get; init; }
    public string? PayPalOrderId { get; init; }
    public string? AuthorizationId { get; init; }
    public DateTimeOffset AuthorizationExpiresAt { get; init; }
    public int ReauthorizationCount { get; init; }

    public string? CaptureId { get; init; }
    public string? CaptureStatus { get; init; }
    public decimal? CapturedGrossAmount { get; init; }
    public decimal? PayPalFee { get; init; }
    public decimal? NetAmount { get; init; }
    public decimal RefundedAmount { get; init; }
    public IReadOnlyList<RefundView> Refunds { get; init; } = Array.Empty<RefundView>();

    public static PaymentView From(Payment payment) => new()
    {
        AuthorizationStatus = payment.AuthorizationStatus.ToString(),
        AuthorizedAmount = payment.AuthorizedAmount,
        PayPalOrderId = payment.PayPalOrderId,
        AuthorizationId = payment.AuthorizationId,
        AuthorizationExpiresAt = payment.AuthorizationExpiresAt,
        ReauthorizationCount = payment.ReauthorizationCount,
        CaptureId = payment.CaptureId,
        CaptureStatus = payment.CaptureStatus?.ToString(),
        CapturedGrossAmount = payment.CapturedGrossAmount,
        PayPalFee = payment.PayPalFeeAmount,
        NetAmount = payment.NetAmount,
        RefundedAmount = payment.RefundedAmount,
        Refunds = payment.Refunds
            .Select(r => new RefundView
            {
                RefundId = r.Id,
                PayPalRefundId = r.PayPalRefundId,
                IdempotencyKey = r.IdempotencyKey,
                Amount = r.Amount,
                Status = r.Status,
                CreatedAt = r.CreatedAt
            })
            .ToList()
    };
}

public class RefundView
{
    public int RefundId { get; init; }
    public string PayPalRefundId { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Safe descriptor of a saved card - never the PAN.</summary>
public class SavedCardView
{
    public int PaymentMethodId { get; init; }
    public string Brand { get; init; } = string.Empty;
    public string Last4 { get; init; } = string.Empty;
    public string Expiry { get; init; } = string.Empty;
    public string? Alias { get; init; }
}
