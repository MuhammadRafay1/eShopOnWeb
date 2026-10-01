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
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>POST /api/orders — place an order from catalog items. Starts in AwaitingPayment. (any authenticated shopper)</summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, IApiOrderService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, IApiOrderService service, ClaimsPrincipal user, CancellationToken ct) =>
            {
                request.CallerId = user.GetBuyerId();
                return await HandleAsync(request, service, ct);
            })
            .Produces<OrderPaymentDto>(StatusCodes.Status201Created)
            .WithTags("OrderEndpoints");
    }

    public Task<IResult> HandleAsync(CreateOrderRequest request, IApiOrderService service) =>
        HandleAsync(request, service, default);

    public async Task<IResult> HandleAsync(CreateOrderRequest request, IApiOrderService service, CancellationToken ct)
    {
        if (request.ShipToAddress is null)
        {
            throw new BadPaymentRequestException("A shipping address is required.");
        }

        var lines = (request.Items ?? new List<OrderLineInput>())
            .Select(i => new OrderLine(i.CatalogItemId, i.Quantity))
            .ToList();

        var address = new Address(
            request.ShipToAddress.Street,
            request.ShipToAddress.City,
            request.ShipToAddress.State,
            request.ShipToAddress.Country,
            request.ShipToAddress.ZipCode);

        var order = await service.PlaceOrderAsync(request.CallerId, lines, address, ct);
        return Results.Created($"api/orders/{order.Id}", order.ToDto());
    }
}

public class CreateOrderRequest
{
    public List<OrderLineInput>? Items { get; set; }
    public ShipToAddressInput? ShipToAddress { get; set; }

    /// <summary>Resolved from the JWT, not the request body.</summary>
    public string CallerId { get; set; } = string.Empty;
}

public class OrderLineInput
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class ShipToAddressInput
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}
