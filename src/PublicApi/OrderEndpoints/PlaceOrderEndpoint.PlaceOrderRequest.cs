using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>Places an order from catalog items. The caller's identity comes from the token.</summary>
public class PlaceOrderRequest : BaseRequest
{
    public List<OrderItemRequest> Items { get; set; } = new();
}

/// <summary>One requested catalog item and quantity.</summary>
public class OrderItemRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}
