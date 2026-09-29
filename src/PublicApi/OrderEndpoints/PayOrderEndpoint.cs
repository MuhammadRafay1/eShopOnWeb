using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
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
/// Authorizes (holds) the order total via PayPal. Pays either with inline card details or with one
/// of the shopper's saved cards. Does not capture - that happens at fulfilment. Idempotent in
/// effect: if the order is already authorized (or beyond), returns the existing payment state
/// without calling PayPal again.
/// </summary>
public class PayOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                int orderId,
                PayOrderRequest request,
                IRepository<Order> orderRepository,
                IRepository<Payment> paymentRepository,
                IReadRepository<Buyer> buyerRepository,
                IPayPalPaymentGateway gateway,
                IOptions<PayPalOptions> payPalOptions,
                ClaimsPrincipal user,
                CancellationToken ct) =>
            {
                return await HandleAsync(orderId, request, orderRepository, paymentRepository,
                    buyerRepository, gateway, payPalOptions, user, ct);
            })
            .Produces<OrderView>()
            .WithTags("OrderEndpoints");
    }

    private static async Task<IResult> HandleAsync(int orderId, PayOrderRequest request,
        IRepository<Order> orderRepository, IRepository<Payment> paymentRepository,
        IReadRepository<Buyer> buyerRepository, IPayPalPaymentGateway gateway,
        IOptions<PayPalOptions> payPalOptions, ClaimsPrincipal user, CancellationToken ct)
    {
        var buyerId = user.Identity!.Name!;
        var currency = payPalOptions.Value.Currency;

        var order = await orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId), ct);
        if (order is null || order.BuyerId != buyerId)
        {
            throw new OrderNotFoundException(orderId);
        }

        var payment = await paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(orderId), ct);

        // Idempotent-in-effect: already authorized (or further along) - never authorize twice.
        if (order.Status != OrderStatus.AwaitingPayment)
        {
            return Results.Ok(OrderView.From(order, payment, currency));
        }

        // Exactly one of card / saved-card must be supplied.
        var hasCard = request.Card is not null;
        var hasSavedCard = request.PaymentMethodId is > 0;
        if (hasCard == hasSavedCard)
        {
            return Results.BadRequest("Provide exactly one of 'card' or 'paymentMethodId'.");
        }

        string? vaultId = null;
        if (hasSavedCard)
        {
            var buyer = await buyerRepository.FirstOrDefaultAsync(
                new BuyerWithPaymentMethodsSpecification(buyerId), ct);
            var method = buyer?.PaymentMethods.FirstOrDefault(pm => pm.Id == request.PaymentMethodId!.Value);
            if (method is null)
            {
                throw new PaymentMethodNotFoundException(request.PaymentMethodId!.Value);
            }
            vaultId = method.CardId;
        }

        var isNewPayment = payment is null;
        payment ??= new Payment(orderId, order.Total(), currency);
        var attempt = payment.IncrementPaymentAttempt();
        var idempotencyKey = PaymentRuntime.BuildAuthorizeKey(orderId, attempt);
        var invoiceId = PaymentRuntime.BuildInvoiceId(orderId, attempt);

        var authorizeRequest = new PayPalAuthorizeRequest
        {
            Amount = order.Total(),
            Currency = currency,
            InvoiceId = invoiceId,
            IdempotencyKey = idempotencyKey,
            Card = request.Card?.ToCardDetails(),
            VaultId = vaultId
        };

        var outcome = await gateway.AuthorizeAsync(authorizeRequest, ct);

        if (outcome.Approved)
        {
            payment.RecordAuthorization(outcome.PayPalOrderId, outcome.AuthorizationId!,
                outcome.Status, outcome.ExpiresAt, invoiceId);
            order.MarkPaymentAuthorized();

            await PersistAsync(paymentRepository, payment, isNewPayment, ct);
            await orderRepository.UpdateAsync(order, ct);

            return Results.Ok(OrderView.From(order, payment, currency));
        }

        // Declined: persist the denied attempt for audit, leave the order AwaitingPayment so the
        // shopper can retry, and surface PayPal's decline reason.
        payment.RecordDeniedAuthorization(outcome.PayPalOrderId);
        await PersistAsync(paymentRepository, payment, isNewPayment, ct);

        throw new PaymentDeclinedException(
            outcome.DeclineReason ?? "PayPal declined the payment.");
    }

    private static async Task PersistAsync(IRepository<Payment> paymentRepository, Payment payment,
        bool isNew, CancellationToken ct)
    {
        if (isNew)
        {
            await paymentRepository.AddAsync(payment, ct);
        }
        else
        {
            await paymentRepository.UpdateAsync(payment, ct);
        }
    }
}

public class PayOrderRequest
{
    /// <summary>Inline card details for a one-off payment. Mutually exclusive with PaymentMethodId.</summary>
    public CardRequestDto? Card { get; set; }

    /// <summary>A saved card to pay with. Mutually exclusive with Card.</summary>
    public int? PaymentMethodId { get; set; }
}
