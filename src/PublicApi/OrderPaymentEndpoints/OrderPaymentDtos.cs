using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class OrderItemDto
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class ShipToAddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string Country { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}

public class OrderLineItemDto
{
    public int CatalogItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Units { get; set; }
}

public class OrderStateDto
{
    public int OrderId { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public decimal Total { get; set; }
    public List<OrderLineItemDto> Items { get; set; } = new();
    public string PaymentStatus { get; set; } = string.Empty;
    public string? AuthorizationId { get; set; }
    public string? CaptureId { get; set; }
    public decimal? CapturedGross { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal RefundedTotal { get; set; }
}

public class ReconciliationEntryDto
{
    public string? PayPalTransactionId { get; set; }
    public string? PayPalStatus { get; set; }
    public decimal? PayPalAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public int? OrderId { get; set; }
    public string? EshopPaymentStatus { get; set; }
    public string MatchState { get; set; } = string.Empty;
}
