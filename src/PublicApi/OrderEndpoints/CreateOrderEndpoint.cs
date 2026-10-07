using System.Collections.Generic;
using System.Linq;
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
using Microsoft.eShopWeb.PublicApi.InvestingEndpoints;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// POST /api/orders — places an order for the signed-in shopper from catalog items, reusing the
/// app's existing order model. The order is treated as paid on placement; for an enrolled shopper the
/// round-up is set aside. Placing the order never fails because of anything to do with investing.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, IInvestingService>
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IAppLogger<CreateOrderEndpoint> _logger;

    public CreateOrderEndpoint(
        IRepository<Order> orderRepository,
        IRepository<CatalogItem> itemRepository,
        IUriComposer uriComposer,
        IAppLogger<CreateOrderEndpoint> logger)
    {
        _orderRepository = orderRepository;
        _itemRepository = itemRepository;
        _uriComposer = uriComposer;
        _logger = logger;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, IInvestingService investingService, HttpContext http) =>
            {
                var shopperId = InvestingShared.ShopperId(http.User);
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();
                request.ShopperId = shopperId;
                return await HandleAsync(request, investingService);
            })
            .Produces<CreateOrderResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, IInvestingService investingService)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return Results.BadRequest("At least one order item is required.");
        }

        if (request.Items.Any(i => i.Quantity <= 0))
        {
            return Results.BadRequest("Every item quantity must be greater than zero.");
        }

        var catalogItemIds = request.Items.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _itemRepository.ListAsync(new CatalogItemsSpecification(catalogItemIds));
        if (catalogItems.Count != catalogItemIds.Length)
        {
            return Results.BadRequest("One or more catalog items do not exist.");
        }

        var orderItems = request.Items.Select(line =>
        {
            var catalogItem = catalogItems.First(c => c.Id == line.CatalogItemId);
            var pictureName = string.IsNullOrEmpty(catalogItem.PictureUri) ? "eCatalog-item-default.png" : catalogItem.PictureUri;
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(pictureName));
            return new OrderItem(itemOrdered, catalogItem.Price, line.Quantity);
        }).ToList();

        // The API has no shipping step; a placeholder address keeps the existing order model satisfied.
        var shipToAddress = new Address("N/A", "N/A", "N/A", "N/A", "00000");
        var order = new Order(request.ShopperId!, shipToAddress, orderItems);
        await _orderRepository.AddAsync(order);

        // The order is paid on placement. Setting the change aside must never break the order.
        decimal roundUpAmount = 0m;
        try
        {
            roundUpAmount = await investingService.ApplyPaidOrderAsync(request.ShopperId!, order.Total());
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning("Round-up skipped for order {OrderId}: {Error}", order.Id, ex.Message);
        }

        return Results.Created($"api/orders/{order.Id}", new CreateOrderResponse(order.Id, roundUpAmount));
    }
}

public class CreateOrderRequest
{
    public List<OrderLineRequest> Items { get; set; } = new();

    [JsonIgnore]
    public string? ShopperId { get; set; }
}

public class OrderLineRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public record CreateOrderResponse(int OrderId, decimal RoundUpAmount);
