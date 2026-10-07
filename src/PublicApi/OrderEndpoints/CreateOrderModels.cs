using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>An order placed from catalog items.</summary>
public class CreateOrderRequest
{
    public List<CreateOrderItemRequest>? Items { get; set; }
}

public class CreateOrderItemRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>The placed order and the spare change it set aside.</summary>
public class CreateOrderResponse
{
    public int OrderId { get; set; }

    /// <summary>The amount this order set aside to invest; 0 when it set aside nothing.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}
