using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CreateOrderItemRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class ShipToAddressRequest
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}

public class CreateOrderBody
{
    public List<CreateOrderItemRequest> Items { get; set; } = new();
    public ShipToAddressRequest ShipToAddress { get; set; } = new();
}

public class CreateOrderRequest : BaseRequest
{
    public string BuyerId { get; }
    public CreateOrderBody Body { get; }

    public CreateOrderRequest(string buyerId, CreateOrderBody body)
    {
        BuyerId = buyerId;
        Body = body;
    }
}
