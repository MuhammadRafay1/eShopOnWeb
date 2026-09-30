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

public class FulfilOrderRequest : BaseRequest
{
    public int OrderId { get; init; }
    public FulfilOrderRequest(int orderId) => OrderId = orderId;
}

public class FulfilOrderResponse : BaseResponse
{
    public FulfilOrderResponse(Guid correlationId) : base(correlationId) { }
    public FulfilOrderResponse() { }

    public int OrderId { get; set; }
    public string Status { get; set; } = "";
    public PaymentDto? Payment { get; set; }
}

/// <summary>
/// POST api/orders/{orderId}/fulfil — operator marks the order fulfilled; that is when the held funds
/// are captured. A stale authorization is renewed first; one that cannot be renewed is reported as such.
/// </summary>
public class FulfilOrderEndpoint : IEndpoint<IResult, FulfilOrderRequest, IRepository<Order>>
{
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IPaymentGatewayService _gateway;

    public FulfilOrderEndpoint(IRepository<Payment> paymentRepository, IPaymentGatewayService gateway)
    {
        _paymentRepository = paymentRepository;
        _gateway = gateway;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/fulfil",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IRepository<Order> orderRepository) =>
            {
                return await HandleAsync(new FulfilOrderRequest(orderId), orderRepository);
            })
            .Produces<FulfilOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(FulfilOrderRequest request, IRepository<Order> orderRepository)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId);
        if (order is null)
            return Results.NotFound();

        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(order.Id));
        var response = new FulfilOrderResponse(request.CorrelationId()) { OrderId = order.Id };

        // Idempotent no-op: already fulfilled → return the existing capture snapshot.
        if (order.Status == OrderStatus.Fulfilled)
        {
            response.Status = order.Status.ToString();
            response.Payment = payment is null ? null : PaymentDto.From(payment);
            return Results.Ok(response);
        }

        if (!order.CurrentlyFulfillable)
            throw new InvalidOrderStateException($"Order {order.Id} cannot be fulfilled from state {order.Status}.");

        if (payment is null)
            throw new InvalidOrderStateException($"Order {order.Id} has no payment to capture.");

        // Renew the authorization if it has gone stale before fulfilment.
        var snapshot = await _gateway.GetAuthorizationStatusAsync(payment.AuthorizationId);
        var status = (snapshot.Status ?? "").ToUpperInvariant();
        bool definitivelyNotRenewable = status is "VOIDED" or "DENIED" or "CAPTURED";
        bool expired = snapshot.ExpiresAt.HasValue && snapshot.ExpiresAt.Value <= DateTimeOffset.UtcNow;

        if (definitivelyNotRenewable)
            throw new PaymentAuthorizationNotRenewableException(
                $"Order {order.Id}'s authorization is {status} and cannot be renewed; cancel this order instead of fulfilling it.");

        if (expired)
        {
            // Throws PaymentAuthorizationNotRenewableException if PayPal refuses the renewal.
            var reauth = await _gateway.ReauthorizeAsync(payment.AuthorizationId, payment.Amount, payment.Currency, order.Id.ToString());
            payment.RecordReauthorization(reauth.AuthorizationId!, reauth.Status ?? "", reauth.ExpiresAt);
        }

        var capture = await _gateway.CaptureAsync(payment.AuthorizationId, payment.Amount, payment.Currency, order.Id.ToString());
        payment.RecordCapture(capture.CaptureId, capture.Status, capture.Amount, capture.PayPalFee, capture.NetAmount);
        await _paymentRepository.UpdateAsync(payment);

        order.MarkFulfilled();
        await orderRepository.UpdateAsync(order);

        response.Status = order.Status.ToString();
        response.Payment = PaymentDto.From(payment);
        return Results.Ok(response);
    }
}
