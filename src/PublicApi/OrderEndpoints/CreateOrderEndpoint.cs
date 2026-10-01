using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.PublicApi.PaymentModels;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// POST api/orders — place an order from catalog items. The caller's identity comes from the JWT; the
/// order starts awaiting payment. Returns the new order's id as a top-level field.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (CreateOrderRequest request, ClaimsPrincipal user, IPaymentService service, CancellationToken ct) =>
            {
                request.BuyerId = user.Identity!.Name!;
                return await Run(request, service, ct);
            })
            .Produces(StatusCodes.Status201Created)
            .WithTags("OrderEndpoints");
    }

    public Task<IResult> HandleAsync(CreateOrderRequest request, IPaymentService service) =>
        Run(request, service, CancellationToken.None);

    private static async Task<IResult> Run(CreateOrderRequest request, IPaymentService service, CancellationToken ct)
    {
        var lines = request.Items.Select(i => new OrderLine(i.CatalogItemId, i.Quantity)).ToList();
        var orderId = await service.PlaceOrderAsync(request.BuyerId, lines, request.ShipToAddress.ToShipTo(), ct);
        return Results.Created($"api/orders/{orderId}", new { orderId, status = "AwaitingPayment" });
    }
}
