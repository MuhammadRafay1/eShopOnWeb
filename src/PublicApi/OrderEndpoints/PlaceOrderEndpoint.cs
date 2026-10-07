using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// POST /api/orders — places an order for the signed-in shopper from catalog item ids and quantities,
/// reusing the shop's existing order/order-item model. The order is treated as paid on placement, so an
/// enrolled shopper's change is set aside here. Placing the order never fails because of investing.
/// </summary>
public class PlaceOrderEndpoint : IEndpoint<IResult, PlaceOrderRequest, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (PlaceOrderRequest request, HttpContext http) => await HandleAsync(request, http))
            .Produces<PlaceOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(PlaceOrderRequest request, HttpContext http)
    {
        var buyerId = http.User.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return Results.BadRequest("At least one order item is required.");
        }

        var orders = http.RequestServices.GetRequiredService<IOrderPlacementService>();
        var investing = http.RequestServices.GetRequiredService<IInvestingService>();

        var lines = request.Items.Select(i => new OrderLine(i.CatalogItemId, i.Quantity));
        var order = await orders.PlaceOrderAsync(buyerId, lines, http.RequestAborted);

        // Setting aside the change must never make this request fail; the service swallows its own errors.
        var roundUp = await investing.SetAsideFromPaidOrderAsync(buyerId, order.Total(), http.RequestAborted);

        var response = new PlaceOrderResponse(request.CorrelationId())
        {
            OrderId = order.Id,
            RoundUpAmount = roundUp
        };
        return Results.Ok(response);
    }
}
