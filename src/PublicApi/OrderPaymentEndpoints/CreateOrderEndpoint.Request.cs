using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class CreateOrderRequest : BaseRequest
{
    public List<OrderItemRequest> Items { get; init; } = new();
    public ShipToAddressRequest? ShipToAddress { get; init; }
}

public class OrderItemRequest
{
    public int CatalogItemId { get; init; }
    public int Quantity { get; init; }
}

public class ShipToAddressRequest
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string ZipCode { get; init; } = string.Empty;
}
