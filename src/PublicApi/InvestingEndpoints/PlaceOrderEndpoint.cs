using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Places and pays for an order from catalog items, reusing the app's order model. For an enrolled
/// and accepted investor it sets aside the round-up to the next whole euro.
/// </summary>
public class PlaceOrderEndpoint : IEndpoint<IResult, PlaceOrderRequest, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PlaceOrderEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (PlaceOrderRequest request, IInvestingService investingService) =>
            {
                return await HandleAsync(request, investingService);
            })
            .Produces<PlaceOrderResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(PlaceOrderRequest request, IInvestingService investingService)
    {
        var buyerId = _httpContextAccessor.HttpContext?.User.GetBuyerId();
        if (string.IsNullOrEmpty(buyerId))
            return Results.Unauthorized();

        if (request.Items is null || request.Items.Count == 0)
            return Results.BadRequest(new { error = "An order must contain at least one item." });

        var lines = request.Items
            .Select(i => new OrderLine(i.CatalogItemId, i.Quantity))
            .ToList();

        try
        {
            var result = await investingService.PlaceOrderAsync(buyerId, lines);
            return Results.Ok(new PlaceOrderResponse(request.CorrelationId())
            {
                OrderId = result.OrderId,
                RoundUpAmount = InvestingShared.ToEuros(result.RoundUpCents)
            });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
