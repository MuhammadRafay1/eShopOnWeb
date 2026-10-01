using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CreateOrderItemRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class CreateOrderRequest : BaseRequest
{
    public List<CreateOrderItemRequest> Items { get; set; } = new();

    public string? ShipToStreet { get; set; }
    public string? ShipToCity { get; set; }
    public string? ShipToState { get; set; }
    public string? ShipToCountry { get; set; }
    public string? ShipToZipCode { get; set; }
}
