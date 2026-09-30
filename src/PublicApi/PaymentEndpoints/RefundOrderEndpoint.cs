using System;
using System.Security.Claims;
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

public class RefundOrderRequest : BaseRequest
{
    /// <summary>Amount to refund. Omit for a full/remaining refund.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Caller-supplied idempotency key: repeating a request under the same key never refunds twice.</summary>
    public string IdempotencyKey { get; set; } = "";

    public int OrderId { get; set; }
    public string BuyerId { get; set; } = "";
}

public class RefundOrderResponse : BaseResponse
{
    public RefundOrderResponse(Guid correlationId) : base(correlationId) { }

    public int RefundId { get; set; }
    public string PayPalRefundId { get; set; } = "";
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public decimal RemainingRefundable { get; set; }
}

/// <summary>Refunds a captured payment, full or partial, deduplicated by the caller-supplied idempotency key.</summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderRequest, IOrderPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderRequest request, ClaimsPrincipal user, IOrderPaymentService paymentService,
             CancellationToken ct) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, paymentService, ct);
            })
            .Produces<RefundOrderResponse>(StatusCodes.Status201Created)
            .WithTags("OrderPaymentEndpoints");
    }

    public Task<IResult> HandleAsync(RefundOrderRequest request, IOrderPaymentService paymentService) =>
        HandleAsync(request, paymentService, CancellationToken.None);

    public async Task<IResult> HandleAsync(RefundOrderRequest request, IOrderPaymentService paymentService, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new InvalidPaymentRequestException("An idempotencyKey is required for a refund.");
        }

        var result = await paymentService.RefundAsync(
            request.OrderId, request.BuyerId, request.Amount, request.IdempotencyKey, ct);

        var response = new RefundOrderResponse(request.CorrelationId())
        {
            RefundId = result.RefundId,
            PayPalRefundId = result.PayPalRefundId,
            OrderId = result.OrderId,
            Amount = result.Amount,
            Status = result.Status.ToString(),
            RemainingRefundable = result.RemainingRefundable
        };
        return Results.Created($"api/orders/{result.OrderId}/refunds/{result.RefundId}", response);
    }
}
