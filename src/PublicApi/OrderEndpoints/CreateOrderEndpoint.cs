using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order from catalog items for the signed-in shopper. For an enrolled, accepted investor the paid
/// order sets aside its round-up; this never causes the order to fail.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, IOrderPlacementService, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, ClaimsPrincipal user, IOrderPlacementService placement, IInvestingService investing) =>
            {
                request.BuyerId = user.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
                return await HandleAsync(request, placement, investing);
            })
            .Produces<CreateOrderResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, IOrderPlacementService placement, IInvestingService investing)
    {
        if (string.IsNullOrEmpty(request.BuyerId))
        {
            return Results.Unauthorized();
        }

        var lines = request.Items.Select(i => new OrderLine(i.CatalogItemId, i.Quantity)).ToList();

        Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate.Order order;
        try
        {
            order = await placement.PlaceOrderAsync(request.BuyerId, lines);
        }
        catch (OrderPlacementException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        // The order is placed and paid. Setting aside its change never throws and never fails the order.
        var roundUp = await investing.SetAsideAndMaybeInvestAsync(order);

        var response = new CreateOrderResponse(request.CorrelationId())
        {
            OrderId = order.Id,
            RoundUpAmount = roundUp
        };
        return Results.Ok(response);
    }
}
