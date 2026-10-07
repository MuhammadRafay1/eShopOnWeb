using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>A line of a <see cref="CreateOrderRequest"/>: a catalog item and how many of it.</summary>
public class OrderItemRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>Places an order from catalog items. The caller's identity comes from the token.</summary>
public class CreateOrderRequest : BaseRequest
{
    public List<OrderItemRequest> Items { get; set; } = new();
}
