using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Configuration;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order for the signed-in shopper from catalog item ids + quantities.
/// The order starts in status AwaitingPayment; pay it with POST api/orders/{orderId}/pay.
/// </summary>
public class PlaceOrderEndpoint : IEndpoint<IResult, PlaceOrderRequest, IApiOrderService, HttpContext>
{
    private readonly PayPalSettings _payPalSettings;

    public PlaceOrderEndpoint(IOptions<PayPalSettings> payPalSettings)
    {
        _payPalSettings = payPalSettings.Value;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (PlaceOrderRequest request, IApiOrderService orderService, HttpContext httpContext) =>
            {
                return await HandleAsync(request, orderService, httpContext);
            })
            .Produces<PlaceOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(PlaceOrderRequest request, IApiOrderService orderService, HttpContext httpContext)
    {
        var response = new PlaceOrderResponse(request.CorrelationId());

        if (request.Items is null || request.Items.Count == 0)
        {
            return Results.BadRequest("An order must contain at least one item.");
        }
        if (request.Items.Any(i => i.Quantity < 1))
        {
            return Results.BadRequest("Quantity must be at least 1 for every item.");
        }

        var buyerId = httpContext.User.Identity!.Name!;

        var shipTo = request.ShipToAddress is not null
            ? new Address(request.ShipToAddress.Street, request.ShipToAddress.City, request.ShipToAddress.State, request.ShipToAddress.Country, request.ShipToAddress.ZipCode)
            : new Address("123 Main St", "Redmond", "WA", "USA", "98052");

        var lines = request.Items
            .Select(i => new OrderLineInput { CatalogItemId = i.CatalogItemId, Quantity = i.Quantity })
            .ToList();

        var order = await orderService.PlaceOrderAsync(buyerId, lines, shipTo, httpContext.RequestAborted);

        response.OrderId = order.Id;
        response.PaymentStatus = order.PaymentStatus.ToString();
        response.Total = order.Total();
        response.Currency = _payPalSettings.Currency;
        response.Items = order.OrderItems
            .Select(oi => new OrderLineDto { CatalogItemId = oi.ItemOrdered.CatalogItemId, Quantity = oi.Units })
            .ToList();

        return Results.Created($"/api/orders/{order.Id}", response);
    }
}
