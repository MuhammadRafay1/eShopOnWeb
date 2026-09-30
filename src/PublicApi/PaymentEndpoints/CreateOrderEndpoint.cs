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
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

public class CreateOrderRequest : BaseRequest
{
    public ShippingAddressDto ShippingAddress { get; set; } = new();
    public List<OrderLineDto> Items { get; set; } = new();
    public string BuyerId { get; set; } = "";
}

public class CreateOrderResponse : BaseResponse
{
    public CreateOrderResponse(System.Guid correlationId) : base(correlationId) { }

    public int OrderId { get; set; }
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public string Currency { get; set; } = "";
    public List<OrderItemSummaryDto> Items { get; set; } = new();
}

/// <summary>Places an order from catalog items for the signed-in shopper. The order starts awaiting payment.</summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, IOrderService>
{
    private readonly PayPalOptions _payPalOptions;

    public CreateOrderEndpoint(IOptions<PayPalOptions> payPalOptions)
    {
        _payPalOptions = payPalOptions.Value;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, ClaimsPrincipal user, IOrderService orderService) =>
            {
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, orderService);
            })
            .Produces<CreateOrderResponse>(StatusCodes.Status201Created)
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, IOrderService orderService)
    {
        var address = new Address(
            request.ShippingAddress.Street,
            request.ShippingAddress.City,
            request.ShippingAddress.State ?? string.Empty,
            request.ShippingAddress.Country,
            request.ShippingAddress.ZipCode);

        var lines = request.Items.Select(i => (i.CatalogItemId, i.Quantity));

        var order = await orderService.CreateOrderFromItemsAsync(
            request.BuyerId, _payPalOptions.Currency, address, lines);

        var response = new CreateOrderResponse(request.CorrelationId())
        {
            OrderId = order.Id,
            Status = order.Status.ToString(),
            Total = order.Total(),
            Currency = order.Currency,
            Items = order.OrderItems.Select(oi => new OrderItemSummaryDto
            {
                CatalogItemId = oi.ItemOrdered.CatalogItemId,
                ProductName = oi.ItemOrdered.ProductName,
                UnitPrice = oi.UnitPrice,
                Units = oi.Units
            }).ToList()
        };

        return Results.Created($"api/orders/{order.Id}", response);
    }
}
