using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.PublicApi.InvestingEndpoints;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Place an order for the signed-in shopper from catalog items. The order is placed (and paid) regardless of
/// investing; for an accepted investor it sets aside the round-up and invests once the threshold is reached.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CreateOrderEndpoint(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, IInvestingService service) => await HandleAsync(request, service))
            .Produces<CreateOrderResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, IInvestingService service)
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var buyerId = httpContext.User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();

        if (request.Items is null || request.Items.Count == 0)
            return Results.BadRequest(new { error = "At least one order item is required." });
        if (request.Items.Any(i => i.Quantity <= 0))
            return Results.BadRequest(new { error = "Each item quantity must be greater than zero." });

        var lines = request.Items.Select(i => new OrderLine(i.CatalogItemId, i.Quantity)).ToList();

        try
        {
            var result = await service.PlaceOrderAsync(buyerId, lines, httpContext.RequestAborted);
            return Results.Ok(new CreateOrderResponse
            {
                OrderId = result.OrderId,
                RoundUpAmount = result.RoundUpAmount
            });
        }
        catch (System.ArgumentException ex)
        {
            // An unknown catalog item id.
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

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}
