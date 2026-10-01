using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class CreateOrderRequest : BaseRequest
{
    public List<OrderItemDto> Items { get; set; } = new();
    public ShipToAddressDto? ShipTo { get; set; }
}

public class CreateOrderResponse : BaseResponse
{
    public int OrderId { get; set; }
}

/// <summary>Places an order from catalog items; the request starts out awaiting payment.</summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, string, IOrderPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, ClaimsPrincipal user, IOrderPaymentService orderPaymentService) =>
            {
                var buyerId = user.FindFirstValue(ClaimTypes.Name)!;
                return await HandleAsync(request, buyerId, orderPaymentService);
            })
            .Produces<CreateOrderResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, string buyerId, IOrderPaymentService orderPaymentService)
    {
        var response = new CreateOrderResponse();

        var items = request.Items.ConvertAll(i => new OrderLineItemRequest(i.CatalogItemId, i.Quantity));
        var shipTo = request.ShipTo is null
            ? null
            : new ShipToAddressRequest(request.ShipTo.Street, request.ShipTo.City, request.ShipTo.State, request.ShipTo.Country, request.ShipTo.ZipCode);

        var orderId = await orderPaymentService.PlaceOrderAsync(buyerId, items, shipTo, default);
        response.OrderId = orderId;

        return Results.Created($"api/my-orders", response);
    }
}
