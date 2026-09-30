using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// POST /api/orders/{orderId}/cancel — operator cancels before fulfilment; any held funds
/// are released (the authorization is voided) so no money ever moved. Operator-only.
/// </summary>
public class CancelOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/cancel",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IPaymentService paymentService, CancellationToken ct) =>
            {
                var result = await paymentService.CancelOrderAsync(orderId, ct);
                return Results.Ok(new CancelOrderResponse
                {
                    OrderId = result.OrderId,
                    OrderStatus = result.OrderStatus,
                    AuthorizationVoided = result.AuthorizationVoided,
                    AlreadyCancelled = result.AlreadyCancelled
                });
            })
            .Produces<CancelOrderResponse>()
            .WithTags("OrderEndpoints");
    }
}

public class CancelOrderResponse
{
    public int OrderId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public bool AuthorizationVoided { get; set; }
    public bool AlreadyCancelled { get; set; }
}
