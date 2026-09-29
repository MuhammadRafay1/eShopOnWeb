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
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Places an order from catalog item ids/quantities for the signed-in shopper. The order starts
/// awaiting payment. Reuses the app's existing Order/OrderItem model.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, CreateOrderRequest, IOrderService>
{
    private readonly string _currency;

    public CreateOrderEndpoint(IOptions<PaymentSettings> paymentSettings)
    {
        _currency = paymentSettings.Value.Currency;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateOrderRequest request, ClaimsPrincipal user, IOrderService orderService) =>
            {
                return await HandleAsync(request, user, orderService);
            })
            .Produces<OrderResponse>(StatusCodes.Status201Created)
            .WithTags("OrderEndpoints");
    }

    public Task<IResult> HandleAsync(CreateOrderRequest request, IOrderService orderService)
        => HandleAsync(request, null!, orderService);

    public async Task<IResult> HandleAsync(CreateOrderRequest request, ClaimsPrincipal user, IOrderService orderService)
    {
        var buyerId = CallerIdentity.GetBuyerId(user);

        var shippingAddress = request.ShippingAddress is null
            ? PlaceholderAddress()
            : new Address(request.ShippingAddress.Street, request.ShippingAddress.City,
                request.ShippingAddress.State, request.ShippingAddress.Country, request.ShippingAddress.ZipCode);

        var items = request.Items
            .Select(i => (i.CatalogItemId, i.Quantity))
            .ToList();

        var order = await orderService.CreatePendingOrderAsync(buyerId, shippingAddress, items);

        var response = OrderResponseMapper.ToResponse(new OrderPaymentResult(order, null), _currency);
        return Results.Created($"api/orders/{order.Id}", response);
    }

    // Mirrors the storefront's own hardcoded checkout address; the task's request only requires
    // catalog item ids + quantities, so a full shipping flow is out of scope.
    private static Address PlaceholderAddress() =>
        new("123 Main St.", "Kent", "OH", "United States", "44240");
}
