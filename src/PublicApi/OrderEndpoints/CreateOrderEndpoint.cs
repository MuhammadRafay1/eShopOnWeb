using System.Collections.Generic;
using System.Linq;
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
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order from catalog items for the signed-in shopper, reusing the
/// app's existing order model. eShopOnWeb has no separate payment step, so the
/// order is treated as paid on placement: an enrolled shopper's spare change is
/// set aside here. Placing the order never fails because of investing.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, IRepository<Order>>
{
    private readonly IRepository<CatalogItem> _catalogItems;
    private readonly IInvestingService _investing;
    private readonly IUriComposer _uriComposer;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // Investing does not require a shipping step; use a fixed placeholder address.
    private static readonly Address DefaultShipToAddress =
        new("123 Main St.", "Kent", "OH", "United States", "44240");

    public CreateOrderEndpoint(
        IRepository<CatalogItem> catalogItems,
        IInvestingService investing,
        IUriComposer uriComposer,
        IHttpContextAccessor httpContextAccessor)
    {
        _catalogItems = catalogItems;
        _investing = investing;
        _uriComposer = uriComposer;
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, IRepository<Order> orderRepository) =>
            {
                return await HandleAsync(request, orderRepository);
            })
            .Produces<CreateOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, IRepository<Order> orderRepository)
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var ct = httpContext.RequestAborted;
        var buyerId = httpContext.User.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var requestedItems = (request.Items ?? new List<OrderItemRequest>())
            .Where(i => i.Quantity > 0)
            .ToList();
        if (requestedItems.Count == 0)
        {
            return Results.BadRequest("An order must contain at least one item with a positive quantity.");
        }

        var ids = requestedItems.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _catalogItems.ListAsync(new CatalogItemsSpecification(ids), ct);
        var byId = catalogItems.ToDictionary(c => c.Id);

        var missing = ids.Where(id => !byId.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            return Results.BadRequest($"Unknown catalog item id(s): {string.Join(", ", missing)}.");
        }

        var orderItems = requestedItems.Select(line =>
        {
            var catalogItem = byId[line.CatalogItemId];
            var itemOrdered = new CatalogItemOrdered(
                catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            return new OrderItem(itemOrdered, catalogItem.Price, line.Quantity);
        }).ToList();

        var order = new Order(buyerId, DefaultShipToAddress, orderItems);
        await orderRepository.AddAsync(order, ct);

        // The order is now paid. Set aside spare change for enrolled investors.
        var roundUp = await _investing.HandleOrderPaidAsync(buyerId, order.Id, order.Total(), ct);

        return Results.Ok(new CreateOrderResponse(request.CorrelationId())
        {
            OrderId = order.Id,
            RoundUpAmount = roundUp
        });
    }
}
