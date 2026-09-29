using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Payments.PayPal;
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order directly from catalog item ids + quantities (no basket). The caller's identity
/// comes from the token. The order starts AwaitingPayment.
/// </summary>
public class CreateOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                CreateOrderRequest request,
                IOrderService orderService,
                IOptions<PayPalOptions> payPalOptions,
                ClaimsPrincipal user) =>
            {
                return await HandleAsync(request, orderService, payPalOptions, user);
            })
            .Produces<CreateOrderResponse>(StatusCodes.Status201Created)
            .WithTags("OrderEndpoints");
    }

    private static async Task<IResult> HandleAsync(CreateOrderRequest request,
        IOrderService orderService, IOptions<PayPalOptions> payPalOptions, ClaimsPrincipal user)
    {
        var buyerId = user.Identity!.Name!;

        if (request.CatalogItems is null || request.CatalogItems.Count == 0)
        {
            return Results.BadRequest("An order must contain at least one item.");
        }
        if (request.ShippingAddress is null)
        {
            return Results.BadRequest("A shipping address is required.");
        }

        var items = request.CatalogItems
            .Select(i => new OrderItemRequest(i.CatalogItemId, i.Quantity))
            .ToList();

        var address = new Address(
            request.ShippingAddress.Street,
            request.ShippingAddress.City,
            request.ShippingAddress.State,
            request.ShippingAddress.Country,
            request.ShippingAddress.ZipCode);

        var order = await orderService.CreateOrderAsync(buyerId, items, address);

        var response = new CreateOrderResponse
        {
            OrderId = order.Id,
            Status = order.Status.ToString(),
            Total = order.Total(),
            Currency = payPalOptions.Value.Currency
        };
        return Results.Created($"api/orders/{order.Id}", response);
    }
}

public class CreateOrderRequest
{
    public List<CreateOrderItemDto> CatalogItems { get; set; } = new();
    public ShippingAddressDto? ShippingAddress { get; set; }
}

public class CreateOrderItemDto
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class ShippingAddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}

public class CreateOrderResponse
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Currency { get; set; } = string.Empty;
}
