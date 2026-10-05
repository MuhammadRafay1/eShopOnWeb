using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.PublicApi.InvestingEndpoints;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order from catalog item ids and quantities for the signed-in shopper (Flow 2). Returns the new
/// order id and the change set aside (0 when nothing was set aside). The order always succeeds regardless of
/// anything to do with investing.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, IOrderPlacementService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CreateOrderEndpoint(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (CreateOrderRequest request, IOrderPlacementService orderPlacementService) => await HandleAsync(request, orderPlacementService))
            .Produces<CreateOrderResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, IOrderPlacementService orderPlacementService)
    {
        var buyerId = CallerId.Resolve(_httpContextAccessor.HttpContext?.User);
        if (string.IsNullOrEmpty(buyerId))
            return Results.Unauthorized();

        if (request?.Items is null || request.Items.Count == 0)
            return Results.BadRequest(new { error = "An order must contain at least one item." });

        var lines = request.Items.Select(i => new OrderLine(i.CatalogItemId, i.Quantity)).ToList();

        try
        {
            var result = await orderPlacementService.PlaceOrderAsync(buyerId, lines, _httpContextAccessor.HttpContext!.RequestAborted);
            // Money amounts are JSON numbers with two decimal places.
            return Results.Ok(new CreateOrderResponse { OrderId = result.OrderId, RoundUpAmount = result.RoundUpAmount + 0.00m });
        }
        catch (OrderItemsRequiredException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOrderLineException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}

public class CreateOrderRequest
{
    public List<CreateOrderItem> Items { get; set; } = new();
}

public class CreateOrderItem
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class CreateOrderResponse
{
    public int OrderId { get; set; }
    public decimal RoundUpAmount { get; set; }
}
