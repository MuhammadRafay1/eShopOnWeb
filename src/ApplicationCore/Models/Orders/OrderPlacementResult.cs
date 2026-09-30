using System;

namespace Microsoft.eShopWeb.ApplicationCore.Models.Orders;

public class OrderPlacementResult
{
    public int OrderId { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public decimal Total { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}
