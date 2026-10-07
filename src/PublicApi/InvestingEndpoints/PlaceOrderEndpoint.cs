using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// POST api/orders — place an order from catalog item ids + quantities for the
/// signed-in shopper. Returns the new order id and the amount set aside for
/// investing (0 when nothing was set aside). Placing the order never fails
/// because of anything to do with investing.
/// </summary>
public class PlaceOrderEndpoint : IEndpoint<IResult, PlaceOrderRequest, IOrderPlacementService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PlaceOrderEndpoint(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (PlaceOrderRequest request, IOrderPlacementService orderPlacement, CancellationToken ct) =>
                await HandleAsync(request, orderPlacement, ct))
            .Produces<PlaceOrderResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(PlaceOrderRequest request, IOrderPlacementService orderPlacement)
        => await HandleAsync(request, orderPlacement, CancellationToken.None);

    private async Task<IResult> HandleAsync(PlaceOrderRequest request, IOrderPlacementService orderPlacement, CancellationToken ct)
    {
        var buyerId = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
            return Results.Unauthorized();

        if (request.Items is null || request.Items.Count == 0)
            return Results.BadRequest("An order must contain at least one item.");

        var lines = request.Items
            .Select(i => new OrderLineRequest(i.CatalogItemId, i.Quantity))
            .ToList();

        try
        {
            var result = await orderPlacement.PlaceOrderAsync(buyerId, lines, ct);
            return Results.Ok(new PlaceOrderResponse(request.CorrelationId())
            {
                OrderId = result.OrderId,
                RoundUpAmount = result.RoundUpAmount
            });
        }
        catch (System.ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }
}
