using System.Linq;
using System.Threading;
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
/// Places an order for the signed-in shopper from catalog items. eShopOnWeb has no separate
/// payment step, so the order is treated as paid immediately: its round-up is set aside towards
/// the shopper's investing (if they are an accepted investor). Setting aside the change can never
/// cause this request to fail. Scoped services are resolved from the request so each request has
/// its own database context.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, HttpContext context) => await HandleAsync(request, context))
            .Produces<CreateOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, HttpContext context)
    {
        var shopperId = context.User.Identity?.Name;
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        if (request.Items is null || request.Items.Count == 0)
        {
            return Results.BadRequest("An order must contain at least one item.");
        }
        if (request.Items.Any(i => i.Quantity <= 0))
        {
            return Results.BadRequest("Item quantities must be greater than zero.");
        }

        var orderPlacementService = context.RequestServices.GetRequiredService<IOrderPlacementService>();
        var investingService = context.RequestServices.GetRequiredService<IInvestingService>();

        var lines = request.Items.Select(i => new OrderLine(i.CatalogItemId, i.Quantity)).ToList();
        var order = await orderPlacementService.PlaceOrderAsync(shopperId, lines, CancellationToken.None);

        // The order is now paid. Set aside its round-up; this never throws back to the caller.
        var roundUp = await investingService.ProcessPaidOrderAsync(shopperId, order.Total(), CancellationToken.None);

        var response = new CreateOrderResponse(request.CorrelationId())
        {
            OrderId = order.Id,
            RoundUpAmount = roundUp,
        };
        return Results.Created($"api/orders/{order.Id}", response);
    }
}
