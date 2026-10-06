using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.eShopWeb.PublicApi.InvestingEndpoints;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Flow 2 — place an order from catalog items for the signed-in shopper. The order is treated as paid on
/// placement; an accepted investor's change is rounded up to the next euro and set aside. Investing never
/// fails the order.
/// </summary>
public class PlaceOrderEndpoint : IEndpoint<IResult>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (PlaceOrderRequest request, ClaimsPrincipal user,
                   IOrderPlacementService orders, IInvestingService investing, CancellationToken ct) =>
            {
                var buyerId = InvestingMapping.BuyerId(user);
                if (string.IsNullOrWhiteSpace(buyerId)) return Results.Unauthorized();

                if (request.Items is null || request.Items.Count == 0)
                    return Results.BadRequest(new { message = "An order must contain at least one item." });

                var lines = request.Items
                    .Select(i => new OrderLine(i.CatalogItemId, i.Quantity))
                    .ToList();

                PlacedOrderResult placed;
                try
                {
                    placed = await orders.PlaceOrderAsync(buyerId, lines, ct);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }

                // The order is placed and the request succeeds regardless of what investing does.
                var roundUpCents = await investing.OnOrderPaidAsync(buyerId, placed.Total, ct);

                return Results.Created($"api/orders/{placed.OrderId}", new PlaceOrderResponse
                {
                    OrderId = placed.OrderId,
                    RoundUpAmount = InvestingMapping.Euros(roundUpCents),
                });
            })
            .Produces<PlaceOrderResponse>(StatusCodes.Status201Created)
            .WithTags("OrderEndpoints");
    }

    public Task<IResult> HandleAsync() => Task.FromResult<IResult>(Results.Empty);
}
