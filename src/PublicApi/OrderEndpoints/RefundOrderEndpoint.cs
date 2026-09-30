using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class RefundOrderRequest : BaseRequest
{
    public decimal? Amount { get; set; }            // omitted = full refund of remaining
    public string IdempotencyKey { get; set; } = "";

    [JsonIgnore] public string BuyerId { get; set; } = "";
    [JsonIgnore] public int OrderId { get; set; }
}

public class RefundOrderResponse : BaseResponse
{
    public RefundOrderResponse(Guid correlationId) : base(correlationId) { }
    public RefundOrderResponse() { }

    public int OrderId { get; set; }
    public int RefundId { get; set; }
    public string Status { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public decimal RemainingRefundable { get; set; }
}

/// <summary>
/// POST api/orders/{orderId}/refunds — refund a captured payment, full or partial. Shopper-scoped and
/// self-service. Repeating a request under the same idempotency key does not refund twice.
/// </summary>
public class RefundOrderEndpoint : IEndpoint<IResult, RefundOrderRequest, IRepository<Order>>
{
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IPaymentGatewayService _gateway;

    public RefundOrderEndpoint(IRepository<Payment> paymentRepository, IPaymentGatewayService gateway)
    {
        _paymentRepository = paymentRepository;
        _gateway = gateway;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/refunds",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, RefundOrderRequest request, ClaimsPrincipal user, IRepository<Order> orderRepository) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, orderRepository);
            })
            .Produces<RefundOrderResponse>(StatusCodes.Status201Created)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(RefundOrderRequest request, IRepository<Order> orderRepository)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId);
        if (order is null || order.BuyerId != request.BuyerId)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Results.BadRequest("An idempotencyKey is required.");
        if (request.Amount is <= 0)
            return Results.BadRequest("Refund amount must be greater than zero when specified.");

        if (!order.CurrentlyRefundable)
            throw new InvalidOrderStateException(
                $"Order {order.Id} cannot be refunded from state {order.Status}.");

        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(order.Id));
        if (payment?.CaptureId is null)
            throw new InvalidOrderStateException($"Order {order.Id} has no captured payment to refund.");

        // Idempotency: a request replayed under the same key returns the original refund unchanged.
        var existing = payment.Refunds.FirstOrDefault(r => r.IdempotencyKey == request.IdempotencyKey);
        if (existing is not null)
        {
            return Results.Created($"api/orders/{order.Id}/refunds/{existing.Id}",
                new RefundOrderResponse(request.CorrelationId())
                {
                    OrderId = order.Id,
                    RefundId = existing.Id,
                    Status = existing.Status,
                    Amount = existing.Amount,
                    Currency = payment.Currency,
                    RemainingRefundable = payment.RemainingRefundable
                });
        }

        // eShop-side guard so a partly-refunded order never becomes refundable beyond what was captured.
        var remaining = payment.RemainingRefundable;
        if (request.Amount is { } requested && requested > remaining)
            return Results.BadRequest(
                $"Refund amount {requested} exceeds the remaining refundable balance of {remaining} {payment.Currency}.");

        var result = await _gateway.RefundAsync(payment.CaptureId, request.Amount, payment.Currency,
            idempotencyKey: $"refund:{order.Id}:{request.IdempotencyKey}");

        var refund = payment.AddRefund(result.RefundId, result.Amount, result.Status, request.IdempotencyKey);
        order.MarkRefunded(isFullRefund: payment.RemainingRefundable <= 0m);

        await _paymentRepository.UpdateAsync(payment);
        await orderRepository.UpdateAsync(order);

        return Results.Created($"api/orders/{order.Id}/refunds/{refund.Id}",
            new RefundOrderResponse(request.CorrelationId())
            {
                OrderId = order.Id,
                RefundId = refund.Id,
                Status = refund.Status,
                Amount = refund.Amount,
                Currency = payment.Currency,
                RemainingRefundable = payment.RemainingRefundable
            });
    }
}
