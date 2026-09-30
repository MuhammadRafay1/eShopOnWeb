using System.Collections.Generic;
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
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// POST /api/orders — a logged-in shopper places an order from catalog items. The order
/// starts awaiting payment. Prices come from the catalog, never from the caller.
/// </summary>
public class CreateOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, IPaymentService paymentService, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var buyerId = CallerIdentity.BuyerId(user);
                var lines = (request.Items ?? new List<CreateOrderItem>())
                    .Select(i => new PlaceOrderLine(i.CatalogItemId, i.Quantity))
                    .ToList();
                var address = new PaymentAddressInput(
                    request.ShipToAddress?.Street, request.ShipToAddress?.City,
                    request.ShipToAddress?.State, request.ShipToAddress?.Country, request.ShipToAddress?.ZipCode);

                var result = await paymentService.PlaceOrderAsync(buyerId, lines, address, ct);

                return Results.Created($"api/orders/{result.OrderId}", new CreateOrderResponse
                {
                    OrderId = result.OrderId,
                    Total = result.Total,
                    Currency = result.Currency
                });
            })
            .Produces<CreateOrderResponse>(StatusCodes.Status201Created)
            .WithTags("OrderEndpoints");
    }
}

public class CreateOrderRequest
{
    public List<CreateOrderItem>? Items { get; set; }
    public CreateOrderAddress? ShipToAddress { get; set; }
}

public class CreateOrderItem
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class CreateOrderAddress
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? ZipCode { get; set; }
}

public class CreateOrderResponse
{
    public int OrderId { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = string.Empty;
}
