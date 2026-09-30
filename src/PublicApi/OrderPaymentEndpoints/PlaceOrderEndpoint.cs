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
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

/// <summary>
/// Places an order from catalog items. The order starts AwaitingPayment.
/// </summary>
public class PlaceOrderEndpoint : IEndpoint<IResult, PlaceOrderRequest, IOrderPaymentService>
{
    private readonly ICurrencyProvider _currencyProvider;

    public PlaceOrderEndpoint(ICurrencyProvider currencyProvider)
    {
        _currencyProvider = currencyProvider;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (PlaceOrderRequest request, ClaimsPrincipal user, IOrderPaymentService orderPaymentService) =>
            {
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, orderPaymentService);
            })
            .Produces<PlaceOrderResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(PlaceOrderRequest request, IOrderPaymentService orderPaymentService)
    {
        var response = new PlaceOrderResponse(request.CorrelationId());

        var lines = request.Items.Select(i => new OrderLineRequest(i.CatalogItemId, i.Quantity)).ToList();
        Address? shipTo = request.ShipToAddress is null
            ? null
            : new Address(request.ShipToAddress.Street, request.ShipToAddress.City, request.ShipToAddress.State, request.ShipToAddress.Country, request.ShipToAddress.ZipCode);

        var order = await orderPaymentService.PlaceOrderAsync(request.BuyerId, lines, shipTo, CancellationToken.None);

        response.OrderId = order.Id;
        response.Status = order.PaymentStatus.ToString();
        response.Total = order.Total();
        response.Currency = _currencyProvider.CurrencyCode;

        return Results.Created($"api/orders/{order.Id}", response);
    }
}
