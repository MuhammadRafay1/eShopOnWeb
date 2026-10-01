using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>
/// POST /api/orders/{orderId}/refunds — refund a captured payment, full or partial, under a caller idempotency
/// key. Repeating under the same key does not refund twice. (operator/admin)
/// </summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/refunds",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderRequest request, IPaymentService service, CancellationToken ct) =>
            {
                request.OrderId = orderId;
                return await HandleAsync(request, service, ct);
            })
            .Produces<RefundResponse>(StatusCodes.Status201Created)
            .WithTags("OrderEndpoints");
    }

    public Task<IResult> HandleAsync(RefundOrderRequest request, IPaymentService service) =>
        HandleAsync(request, service, default);

    public async Task<IResult> HandleAsync(RefundOrderRequest request, IPaymentService service, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new BadPaymentRequestException("A refund requires an idempotencyKey.");
        }

        var refund = await service.RefundAsync(request.OrderId, request.Amount, request.IdempotencyKey, ct);

        var response = new RefundResponse
        {
            RefundId = refund.PayPalRefundId,
            Status = refund.Status,
            Amount = refund.Amount,
            OrderId = request.OrderId
        };
        return Results.Created($"api/orders/{request.OrderId}/refunds/{refund.Id}", response);
    }
}

public class RefundOrderRequest
{
    /// <summary>The amount to refund. Omit for a full refund of the remaining refundable amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Caller-supplied idempotency key. Repeating under the same key must not refund twice.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public int OrderId { get; set; }
}

public class RefundResponse
{
    public string? RefundId { get; set; }
    public string? Status { get; set; }
    public decimal Amount { get; set; }
    public int OrderId { get; set; }
}
