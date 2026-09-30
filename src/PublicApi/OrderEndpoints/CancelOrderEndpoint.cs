using System;
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

public class CancelOrderRequest : BaseRequest
{
    public int OrderId { get; init; }
    public CancelOrderRequest(int orderId) => OrderId = orderId;
}

public class CancelOrderResponse : BaseResponse
{
    public CancelOrderResponse(Guid correlationId) : base(correlationId) { }
    public CancelOrderResponse() { }

    public int OrderId { get; set; }
    public string Status { get; set; } = "";
}

/// <summary>
/// POST api/orders/{orderId}/cancel — operator cancels an order before fulfilment; any held funds are
/// released so no money ever moved.
/// </summary>
public class CancelOrderEndpoint : IEndpoint<IResult, CancelOrderRequest, IRepository<Order>>
{
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IPaymentGatewayService _gateway;

    public CancelOrderEndpoint(IRepository<Payment> paymentRepository, IPaymentGatewayService gateway)
    {
        _paymentRepository = paymentRepository;
        _gateway = gateway;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/cancel",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IRepository<Order> orderRepository) =>
            {
                return await HandleAsync(new CancelOrderRequest(orderId), orderRepository);
            })
            .Produces<CancelOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CancelOrderRequest request, IRepository<Order> orderRepository)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId);
        if (order is null)
            return Results.NotFound();

        var response = new CancelOrderResponse(request.CorrelationId()) { OrderId = order.Id };

        // Idempotent no-op.
        if (order.Status == OrderStatus.Cancelled)
        {
            response.Status = order.Status.ToString();
            return Results.Ok(response);
        }

        if (!order.CurrentlyCancellable)
            throw new InvalidOrderStateException(
                $"Order {order.Id} cannot be cancelled from state {order.Status}; use refund instead.");

        // Release the hold at PayPal only if funds were actually held.
        if (order.Status == OrderStatus.PaymentAuthorized)
        {
            var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(order.Id));
            if (payment is not null)
            {
                await _gateway.VoidAuthorizationAsync(payment.AuthorizationId, order.Id.ToString());
                payment.UpdateAuthorizationStatus("VOIDED");
                await _paymentRepository.UpdateAsync(payment);
            }
        }

        order.MarkCancelled();
        await orderRepository.UpdateAsync(order);

        response.Status = order.Status.ToString();
        return Results.Ok(response);
    }
}
