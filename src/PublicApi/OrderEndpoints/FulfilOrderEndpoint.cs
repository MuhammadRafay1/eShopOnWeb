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
/// POST /api/orders/{orderId}/fulfil — operator marks the order fulfilled; this is when the
/// money is actually captured. A stale authorization is renewed first; one that can no longer
/// be renewed yields an operator-actionable 422. Operator-only (Administrators role).
/// </summary>
public class FulfilOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/fulfil",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IPaymentService paymentService, CancellationToken ct) =>
            {
                var result = await paymentService.FulfilOrderAsync(orderId, ct);
                return Results.Ok(new FulfilOrderResponse
                {
                    OrderId = result.OrderId,
                    OrderStatus = result.OrderStatus,
                    CaptureId = result.CaptureId,
                    CaptureStatus = result.CaptureStatus,
                    CapturedAmount = result.CapturedAmount,
                    PayPalFee = result.PayPalFee,
                    NetAmount = result.NetAmount,
                    Currency = result.Currency,
                    Reauthorized = result.Reauthorized,
                    AlreadyFulfilled = result.AlreadyFulfilled
                });
            })
            .Produces<FulfilOrderResponse>()
            .WithTags("OrderEndpoints");
    }
}

public class FulfilOrderResponse
{
    public int OrderId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string CaptureId { get; set; } = string.Empty;
    public string CaptureStatus { get; set; } = string.Empty;
    public decimal CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool Reauthorized { get; set; }
    public bool AlreadyFulfilled { get; set; }
}
