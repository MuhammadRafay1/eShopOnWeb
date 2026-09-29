using System.Threading;
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
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure.Payments.PayPal;
using Microsoft.eShopWeb.PublicApi.PaymentModels;
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Operator action: cancels an order before fulfilment, releasing any held funds so no money ever
/// moves. Admin-only.
/// </summary>
public class CancelOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/cancel",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                int orderId,
                IRepository<Order> orderRepository,
                IRepository<Payment> paymentRepository,
                IPayPalPaymentGateway gateway,
                IOptions<PayPalOptions> payPalOptions,
                CancellationToken ct) =>
            {
                return await HandleAsync(orderId, orderRepository, paymentRepository, gateway,
                    payPalOptions, ct);
            })
            .Produces<OrderView>()
            .WithTags("OrderEndpoints");
    }

    private static async Task<IResult> HandleAsync(int orderId, IRepository<Order> orderRepository,
        IRepository<Payment> paymentRepository, IPayPalPaymentGateway gateway,
        IOptions<PayPalOptions> payPalOptions, CancellationToken ct)
    {
        var currency = payPalOptions.Value.Currency;

        var order = await orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId), ct);
        if (order is null)
        {
            throw new OrderNotFoundException(orderId);
        }

        // Idempotent no-op if already cancelled.
        var payment = await paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(orderId), ct);
        if (order.Status == OrderStatus.Cancelled)
        {
            return Results.Ok(OrderView.From(order, payment, currency));
        }
        if (order.Status != OrderStatus.AwaitingPayment && order.Status != OrderStatus.PaymentAuthorized)
        {
            throw new InvalidOrderStateException(
                $"Order {orderId} cannot be cancelled from status {order.Status}; cancellation is only allowed before fulfilment.");
        }

        // Release the hold only when there is a live authorization to void.
        if (payment is not null &&
            order.Status == OrderStatus.PaymentAuthorized &&
            (payment.AuthorizationStatus == PaymentAuthorizationStatus.Created ||
             payment.AuthorizationStatus == PaymentAuthorizationStatus.Pending))
        {
            await gateway.VoidAsync(payment.AuthorizationId, ct);
            payment.RecordVoid();
            await paymentRepository.UpdateAsync(payment, ct);
        }

        order.MarkCancelled();
        await orderRepository.UpdateAsync(order, ct);

        return Results.Ok(OrderView.From(order, payment, currency));
    }
}
