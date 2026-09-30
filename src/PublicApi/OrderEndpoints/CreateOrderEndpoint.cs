using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure;
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CreateOrderItem
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class ShippingAddressDto
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? ZipCode { get; set; }
}

public class CreateOrderRequest : BaseRequest
{
    public List<CreateOrderItem> Items { get; set; } = new();
    public ShippingAddressDto? ShippingAddress { get; set; }

    [JsonIgnore] // never bound from the body — always set from the JWT identity
    public string BuyerId { get; set; } = "";
}

public class CreateOrderResponse : BaseResponse
{
    public CreateOrderResponse(System.Guid correlationId) : base(correlationId) { }
    public CreateOrderResponse() { }

    public int OrderId { get; set; }
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public string Currency { get; set; } = "";
    public List<OrderItemDto> Items { get; set; } = new();
}

/// <summary>
/// POST api/orders — place an order from catalog items. Reuses the existing Order/OrderItem model;
/// the order starts awaiting payment. Any authenticated shopper; acts as the caller.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, IRepository<Order>>
{
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IUriComposer _uriComposer;
    private readonly PayPalOptions _payPalOptions;

    public CreateOrderEndpoint(IRepository<CatalogItem> itemRepository, IUriComposer uriComposer,
        IOptions<PayPalOptions> payPalOptions)
    {
        _itemRepository = itemRepository;
        _uriComposer = uriComposer;
        _payPalOptions = payPalOptions.Value;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, ClaimsPrincipal user, IRepository<Order> orderRepository) =>
            {
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, orderRepository);
            })
            .Produces<CreateOrderResponse>(StatusCodes.Status201Created)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, IRepository<Order> orderRepository)
    {
        var response = new CreateOrderResponse(request.CorrelationId());

        if (request.Items is null || request.Items.Count == 0)
            return Results.BadRequest("At least one order item is required.");
        if (request.Items.Any(i => i.Quantity <= 0))
            return Results.BadRequest("Every order item quantity must be greater than zero.");

        var ids = request.Items.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _itemRepository.ListAsync(new CatalogItemsSpecification(ids));

        var missing = ids.Where(id => catalogItems.All(c => c.Id != id)).ToArray();
        if (missing.Length > 0)
            return Results.BadRequest($"Unknown catalog item id(s): {string.Join(", ", missing)}.");

        var items = request.Items.Select(reqItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == reqItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name,
                _uriComposer.ComposePicUri(catalogItem.PictureUri));
            return new OrderItem(itemOrdered, catalogItem.Price, reqItem.Quantity);
        }).ToList();

        var address = request.ShippingAddress is { } a
            ? new Address(a.Street ?? "", a.City ?? "", a.State ?? "", a.Country ?? "", a.ZipCode ?? "")
            : new Address("", "", "", "", "");

        var order = new Order(request.BuyerId, address, items);
        order = await orderRepository.AddAsync(order);

        response.OrderId = order.Id;
        response.Status = order.Status.ToString();
        response.Total = order.Total();
        response.Currency = _payPalOptions.Currency;
        response.Items = PaymentDto.ItemsOf(order);

        return Results.Created($"api/orders/{order.Id}", response);
    }
}
