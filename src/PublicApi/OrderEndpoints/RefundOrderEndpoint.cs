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

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// POST /api/orders/{orderId}/refunds — refund a captured payment, full or partial.
/// Shopper-scoped (the task lists only fulfil/cancel/reconciliation as operator actions,
/// and states every other endpoint is shopper-scoped): the caller must own the order.
/// The caller-supplied idempotencyKey makes replays return the same refund; a distinct key
/// for a distinct partial amount is a new, legitimate refund. Never refundable beyond capture.
/// </summary>
public class RefundOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderRequest request, IPaymentService paymentService, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var buyerId = CallerIdentity.BuyerId(user);
                var result = await paymentService.RefundOrderAsync(buyerId, orderId, request.Amount, request.IdempotencyKey ?? string.Empty, ct);

                return Results.Ok(new RefundOrderResponse
                {
                    RefundId = result.RefundId,
                    Status = result.Status,
                    Amount = result.Amount,
                    TotalRefunded = result.TotalRefunded,
                    OrderStatus = result.OrderStatus,
                    AlreadyProcessed = result.AlreadyProcessed
                });
            })
            .Produces<RefundOrderResponse>()
            .WithTags("OrderEndpoints");
    }
}

public class RefundOrderRequest
{
    /// <summary>Omit to refund the full remaining amount; supply to refund a partial amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Caller-supplied key; a repeat under the same key never refunds twice.</summary>
    public string? IdempotencyKey { get; set; }
}

public class RefundOrderResponse
{
    public string RefundId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TotalRefunded { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public bool AlreadyProcessed { get; set; }
}
