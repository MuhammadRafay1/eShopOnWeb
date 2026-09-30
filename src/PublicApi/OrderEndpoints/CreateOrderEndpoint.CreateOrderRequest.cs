using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CreateOrderRequest : BaseRequest
{
    public List<OrderLineRequest> Items { get; set; } = new();
    public AddressRequestDto? ShipToAddress { get; set; }

    /// <summary>Set by the endpoint from the caller's JWT identity - never trust a client-supplied buyer id.</summary>
    public string BuyerId { get; set; } = default!;
}

public class OrderLineRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class AddressRequestDto
{
    public string Street { get; set; } = default!;
    public string City { get; set; } = default!;
    public string State { get; set; } = default!;
    public string Country { get; set; } = default!;
    public string ZipCode { get; set; } = default!;
}
