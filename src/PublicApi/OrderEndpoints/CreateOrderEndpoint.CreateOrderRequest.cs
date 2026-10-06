using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CreateOrderRequest : BaseRequest
{
    /// <summary>The catalog items and quantities to order.</summary>
    public List<OrderItemRequest> Items { get; set; } = new();
}

public class OrderItemRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}
