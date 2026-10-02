using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>POST /api/orders — places an order (awaiting payment) from catalog items and quantities.</summary>
public class PlaceOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                PlaceOrderRequest request,
                ClaimsPrincipal user,
                IOrderPaymentService service,
                CancellationToken cancellationToken) =>
            {
                var buyerId = PaymentUser.BuyerId(user);
                var items = (request.Items ?? new()).Select(i => new OrderLineInput(i.CatalogItemId, i.Quantity)).ToList();
                var address = request.ShipToAddress is null
                    ? null
                    : new ShippingAddressInput(request.ShipToAddress.Street, request.ShipToAddress.City,
                        request.ShipToAddress.State, request.ShipToAddress.Country, request.ShipToAddress.ZipCode);

                var orderId = await service.PlaceOrderAsync(buyerId, items, address, cancellationToken);
                return Results.Created($"/api/orders/{orderId}", new { orderId });
            })
            .WithTags("PaymentEndpoints");
    }
}
