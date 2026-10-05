using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.eShopWeb.PublicApi.InvestingEndpoints;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order from catalog items for the signed-in shopper (POST /api/orders), reusing the app's
/// existing order/order-item model. For an enrolled, accepted investor it sets aside the round-up. Placing
/// the order never fails because of anything investing-related.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (CreateOrderRequest request, HttpContext http) => await HandleAsync(request, http))
            .Produces<CreateOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, HttpContext http)
    {
        var shopperId = http.User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        if (request?.Items is null || request.Items.Count == 0)
            return Results.BadRequest(new { error = "items must contain at least one catalog item." });

        var lines = request.Items.Select(i => new OrderLine(i.CatalogItemId, i.Quantity)).ToList();

        var service = http.RequestServices.GetRequiredService<IInvestingService>();
        try
        {
            var result = await service.PlaceOrderAsync(shopperId, lines, http.RequestAborted);
            return Results.Ok(new CreateOrderResponse
            {
                OrderId = result.OrderId,
                RoundUpAmount = result.RoundUpAmount,
            });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
