using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

/// <summary>One caller's order, with its payment state, for GET /api/my-orders.</summary>
public class OrderPaymentSummary
{
    public int OrderId { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public decimal Total { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AuthorizationId { get; set; }
    public string? CaptureId { get; set; }
    public decimal? CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }
    public IReadOnlyList<RefundSummary> Refunds { get; set; } = Array.Empty<RefundSummary>();
}
