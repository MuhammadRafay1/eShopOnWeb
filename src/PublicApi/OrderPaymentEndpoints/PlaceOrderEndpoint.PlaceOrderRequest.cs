using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class PlaceOrderRequest : BaseRequest
{
    public string BuyerId { get; set; } = string.Empty;
    public List<OrderLineDto> Items { get; set; } = new();
    public ShipToAddressDto? ShipToAddress { get; set; }
}
