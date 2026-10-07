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
using Microsoft.eShopWeb.PublicApi.InvestingEndpoints;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order from catalog items for the signed-in shopper, reusing the app's existing order
/// model. eShopOnWeb has no separate payment step, so the order is treated as paid here: once placed,
/// an enrolled shopper's spare change is set aside. Setting aside change never fails the order.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, IInvestingService>
{
    private const string DefaultPictureUri = "eCatalog-item-default.png";

    // API orders carry no shipping address; the order model requires one. Each order gets its own
    // instance because the address is an owned entity (a shared instance cannot back two orders).
    private static Address NewPlaceholderAddress() => new("Online order", "Online", string.Empty, "N/A", "00000");

    private readonly IRepository<Order> _orders;
    private readonly IRepository<CatalogItem> _items;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAppLogger<CreateOrderEndpoint> _logger;

    public CreateOrderEndpoint(
        IRepository<Order> orders,
        IRepository<CatalogItem> items,
        IHttpContextAccessor httpContextAccessor,
        IAppLogger<CreateOrderEndpoint> logger)
    {
        _orders = orders;
        _items = items;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (CreateOrderRequest request, IInvestingService investing) =>
                await HandleAsync(request, investing))
            .Produces<CreateOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateOrderRequest request, IInvestingService investing)
    {
        var buyerId = CallerIdentity.GetBuyerId(_httpContextAccessor);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        if (request.Items == null || request.Items.Count == 0 || request.Items.Any(i => i.Quantity <= 0))
        {
            return Results.BadRequest("An order must contain at least one item, each with a positive quantity.");
        }

        var ids = request.Items.Select(i => i.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _items.ListAsync(new CatalogItemsSpecification(ids));
        var byId = catalogItems.ToDictionary(c => c.Id);

        if (request.Items.Any(i => !byId.ContainsKey(i.CatalogItemId)))
        {
            return Results.BadRequest("One or more catalog items could not be found.");
        }

        var orderItems = request.Items.Select(line =>
        {
            var catalogItem = byId[line.CatalogItemId];
            var pictureUri = string.IsNullOrEmpty(catalogItem.PictureUri) ? DefaultPictureUri : catalogItem.PictureUri;
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, pictureUri);
            return new OrderItem(itemOrdered, catalogItem.Price, line.Quantity);
        }).ToList();

        var order = new Order(buyerId, NewPlaceholderAddress(), orderItems);
        order = await _orders.AddAsync(order);

        // The order is now placed and paid. Setting aside the round-up must never fail the order.
        decimal roundUp = 0m;
        try
        {
            roundUp = await investing.RecordPaidOrderAsync(buyerId, order.Id, order.Total());
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning("Round-up for order {OrderId} failed ({Error}); the order still succeeded.", order.Id, ex.GetType().Name);
        }

        var response = new CreateOrderResponse(request.CorrelationId())
        {
            OrderId = order.Id,
            RoundUpAmount = roundUp,
        };
        return Results.Created($"api/orders/{order.Id}", response);
    }
}
