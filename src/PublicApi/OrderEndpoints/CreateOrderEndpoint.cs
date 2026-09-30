using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Ardalis.Result;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order from catalog items for the signed-in shopper. The order starts in the
/// AwaitingPayment state - see PayOrderEndpoint to authorize payment for it.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, IOrderPlacementService>
{
    public void AddRoute(IEndpointRouteBuilder app) =>
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, ClaimsPrincipal user, IOrderPlacementService orderPlacementService) =>
            {
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, orderPlacementService);
            })
            .Produces<CreateOrderResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("OrderEndpoints");

    public async Task<IResult> HandleAsync(CreateOrderRequest request, IOrderPlacementService orderPlacementService)
    {
        var response = new CreateOrderResponse(request.CorrelationId());

        var items = request.Items.Select(i => new OrderItemRequestLine(i.CatalogItemId, i.Quantity)).ToList();
        var shipToAddress = request.ShipToAddress is null
            ? null
            : new Address(request.ShipToAddress.Street, request.ShipToAddress.City, request.ShipToAddress.State, request.ShipToAddress.Country, request.ShipToAddress.ZipCode);

        var result = await orderPlacementService.PlaceOrderAsync(request.BuyerId, items, shipToAddress);
        if (result.Status != ResultStatus.Ok)
        {
            return result.ToErrorResult();
        }

        response.OrderId = result.Value.Id;
        response.Order = OrderDto.FromDomain(result.Value);
        return Results.Created($"api/orders/{result.Value.Id}", response);
    }
}
