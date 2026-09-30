using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CreateOrderItemDto
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}

public class CreateOrderRequest : BaseRequest
{
    /// <summary>Set server-side from the caller's JWT after binding; any client-supplied value is ignored.</summary>
    public string BuyerId { get; set; } = string.Empty;

    public List<CreateOrderItemDto> Items { get; set; } = new();
    public AddressDto? ShipToAddress { get; set; }
}
