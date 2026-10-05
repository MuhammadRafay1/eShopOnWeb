using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.PublicApi.Investing;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>An order placed from catalog items.</summary>
public class PlaceOrderRequest
{
    public List<OrderLine> Items { get; set; } = new();
}

public class OrderLine
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class PlaceOrderResponse
{
    public int OrderId { get; set; }

    /// <summary>The amount this order set aside towards investing (0 when it set aside nothing).</summary>
    [JsonConverter(typeof(TwoDecimalMoneyConverter))]
    public decimal RoundUpAmount { get; set; }
}
