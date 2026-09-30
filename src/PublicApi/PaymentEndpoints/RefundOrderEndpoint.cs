using System.Security.Claims;
using System.Text.Json.Serialization;
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

public class RefundOrderRequest
{
    /// <summary>Caller-supplied idempotency key: a repeat under the same key never refunds twice.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Amount to refund; omit for a full refund of the remaining captured amount.</summary>
    public decimal? Amount { get; set; }

    [JsonIgnore] public int OrderId { get; set; }
    [JsonIgnore] public string BuyerId { get; set; } = string.Empty;
}

public class RefundOrderResponse
{
    public int RefundId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal RemainingRefundable { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
}

/// <summary>
/// Refunds a fulfilled order's captured payment, in full or in part. Idempotent by the caller's
/// key; never refundable beyond what was captured. Shopper-scoped: acts only on the caller's order.
/// </summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderRequest request, ClaimsPrincipal user, IPaymentService paymentService, CancellationToken ct) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, paymentService, ct);
            })
            .Produces<RefundOrderResponse>(StatusCodes.Status201Created)
            .WithTags("Orders");
    }

    public Task<IResult> HandleAsync(RefundOrderRequest request, IPaymentService paymentService)
        => HandleAsync(request, paymentService, CancellationToken.None);

    public async Task<IResult> HandleAsync(RefundOrderRequest request, IPaymentService paymentService, CancellationToken ct)
    {
        var receipt = await paymentService.RefundOrderAsync(
            request.OrderId, request.BuyerId, request.IdempotencyKey, request.Amount, ct);

        var response = new RefundOrderResponse
        {
            RefundId = receipt.RefundId,
            Status = receipt.Status,
            Amount = receipt.Amount,
            Currency = receipt.Currency,
            RemainingRefundable = receipt.RemainingRefundable,
            OrderStatus = receipt.OrderStatus
        };
        return Results.Created($"api/orders/{request.OrderId}/refunds/{receipt.RefundId}", response);
    }
}
