using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order from catalog items (POST api/orders). Reuses the app's existing
/// order/order-item model. For an enrolled shopper the order is treated as paid, so the
/// rounding difference up to the next whole euro is set aside towards investing. Setting
/// aside change never makes placing the order fail.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, string, IOrderPlacementService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, ClaimsPrincipal user, IOrderPlacementService orderPlacement) =>
            {
                var buyerId = user.FindFirstValue(ClaimTypes.Name);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Results.Unauthorized();
                }

                return await HandleAsync(request, buyerId, orderPlacement);
            })
            .Produces<CreateOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, string buyerId, IOrderPlacementService orderPlacement)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return Results.BadRequest("An order must contain at least one item.");
        }

        var lines = request.Items
            .Select(i => new OrderLine(i.CatalogItemId, i.Quantity))
            .ToList();

        OrderPlacementResult result;
        try
        {
            result = await orderPlacement.PlaceAsync(buyerId, lines);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        return Results.Ok(new CreateOrderResponse(request.CorrelationId())
        {
            OrderId = result.OrderId,
            RoundUpAmount = result.RoundUpAmount,
        });
    }
}
