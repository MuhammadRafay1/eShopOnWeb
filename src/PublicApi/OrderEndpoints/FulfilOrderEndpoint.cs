using System.Security.Claims;
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
/// Operator action: marks the order fulfilled, which is when the money is actually captured.
/// A stale authorization is renewed (reauthorized) and the capture retried once, rather than
/// failing the fulfilment outright; one that can no longer be renewed surfaces PayPal's own
/// reason so an operator can act on it. Admin-only.
/// </summary>
public class FulfilOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/fulfil",
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

        var payment = await paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(orderId), ct);

        // Idempotent no-op: already fulfilled - return the existing capture state.
        if (order.Status == OrderStatus.Fulfilled)
        {
            return Results.Ok(OrderView.From(order, payment, currency));
        }
        if (order.Status != OrderStatus.PaymentAuthorized || payment is null)
        {
            throw new InvalidOrderStateException(
                $"Order {orderId} cannot be fulfilled from status {order.Status}; it must be PaymentAuthorized.");
        }

        var amount = order.Total();
        var invoiceId = payment.InvoiceId ?? orderId.ToString();
        var captureKey = PaymentModels.PaymentRuntime.BuildCaptureKey(orderId, payment.ReauthorizationCount);
        var capture = await gateway.CaptureAsync(payment.AuthorizationId, amount, currency,
            invoiceId, captureKey, ct);

        if (capture.Result == PayPalCaptureResult.AuthorizationExpired)
        {
            // Renew the stale authorization, then retry the capture once with a fresh key.
            var reauth = await gateway.ReauthorizeAsync(payment.AuthorizationId, amount, currency, ct);
            if (reauth.Result == PayPalReauthorizeResult.Failed)
            {
                throw new PaymentRenewalFailedException(
                    $"Order {orderId}'s authorization has expired and could not be renewed: " +
                    $"{reauth.FailureDescription ?? "no further detail from PayPal"}. " +
                    "The shopper must re-pay (cancel and place/pay a new order).");
            }

            payment.RecordReauthorization(reauth.Status, reauth.ExpiresAt);
            await paymentRepository.UpdateAsync(payment, ct);

            var retryKey = PaymentModels.PaymentRuntime.BuildCaptureKey(orderId, payment.ReauthorizationCount);
            capture = await gateway.CaptureAsync(payment.AuthorizationId, amount, currency,
                invoiceId, retryKey, ct);
        }

        if (capture.Result != PayPalCaptureResult.Completed)
        {
            // Leave the order PaymentAuthorized so an operator can decide the next step.
            throw new PaymentDeclinedException(
                $"Capture failed for order {orderId}: {capture.FailureDescription ?? "PayPal did not complete the capture."}");
        }

        payment.RecordCapture(capture.CaptureId!, capture.Status ?? PaymentCaptureStatus.Completed,
            capture.GrossAmount, capture.FeeAmount, capture.NetAmount);
        order.MarkFulfilled();

        await paymentRepository.UpdateAsync(payment, ct);
        await orderRepository.UpdateAsync(order, ct);

        return Results.Ok(OrderView.From(order, payment, currency));
    }
}
