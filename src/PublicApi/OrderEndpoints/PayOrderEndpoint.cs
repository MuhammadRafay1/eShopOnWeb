using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class PayOrderCardRequest
{
    public string Number { get; set; } = string.Empty;
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public string SecurityCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PayOrderBillingAddressRequest BillingAddress { get; set; } = new();
}

public class PayOrderBillingAddressRequest
{
    public string CountryCode { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AdminArea1 { get; set; }
    public string? AdminArea2 { get; set; }
    public string? PostalCode { get; set; }
}

public class PayOrderRequest
{
    public int OrderId { get; set; }
    public string BuyerId { get; set; } = string.Empty;

    /// <summary>One-off card details. Mutually exclusive with <see cref="PaymentMethodId"/>.</summary>
    public PayOrderCardRequest? Card { get; set; }

    /// <summary>Pay with a previously-saved card belonging to the caller.</summary>
    public int? PaymentMethodId { get; set; }
}

public class PayOrderResponse
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AuthorizationId { get; set; }
    public string? AuthorizationStatus { get; set; }
    public decimal AuthorizedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary>
/// Authorizes (holds) the order total with PayPal - never captures. Accepts either a one-off
/// card or a saved payment method belonging to the caller. Idempotent: replaying against an
/// already-authorized order returns the existing hold instead of creating a new one.
/// </summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, OrderEndpointServices>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, ClaimsPrincipal user, OrderEndpointServices services) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, services);
            })
            .Produces<PayOrderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status502BadGateway)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(PayOrderRequest request, OrderEndpointServices services)
    {
        var order = await services.OrderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(request.OrderId));
        if (order is null || order.BuyerId != request.BuyerId)
            return Results.NotFound();

        var existingPayment = await services.PaymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(request.OrderId));

        if (order.Status == OrderStatus.Authorized && existingPayment is not null)
        {
            // Idempotent replay: the order is already authorized, return the existing hold.
            return Results.Ok(ToResponse(order, existingPayment, services.PaymentGateway.Currency));
        }

        if (order.Status != OrderStatus.AwaitingPayment)
            return Results.Conflict($"Order {order.Id} is {order.Status} and cannot be paid.");

        if (request.Card is null && request.PaymentMethodId is null)
            return Results.BadRequest("Either card details or a paymentMethodId is required.");
        if (request.Card is not null && request.PaymentMethodId is not null)
            return Results.BadRequest("Provide either card details or a paymentMethodId, not both.");

        CardDetails? card = null;
        string? vaultId = null;

        if (request.PaymentMethodId is { } paymentMethodId)
        {
            var buyer = await services.BuyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpecification(request.BuyerId));
            var paymentMethod = buyer?.PaymentMethods.FirstOrDefault(p => p.Id == paymentMethodId);
            if (paymentMethod is null)
                return Results.NotFound("Saved payment method not found.");
            vaultId = paymentMethod.CardId;
        }
        else
        {
            var c = request.Card!;
            card = new CardDetails(
                c.Number,
                c.ExpiryMonth,
                c.ExpiryYear,
                c.SecurityCode,
                c.Name,
                new BillingAddress(c.BillingAddress.CountryCode, c.BillingAddress.AddressLine1, c.BillingAddress.AdminArea1, c.BillingAddress.AdminArea2, c.BillingAddress.PostalCode));
        }

        var amount = order.Total();
        var invoiceId = $"eshop-{order.Id}-{Guid.NewGuid():N}";

        PayPalAuthorizationResult authorization;
        try
        {
            authorization = await services.PaymentGateway.AuthorizeAsync(new PayPalAuthorizeRequest
            {
                OrderId = order.Id,
                InvoiceId = invoiceId,
                Amount = amount,
                Card = card,
                VaultId = vaultId
            });
        }
        catch (PayPalActionRequiredException ex)
        {
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
        }
        catch (PayPalGatewayException ex)
        {
            return Results.Json(new { message = ex.Message, debugId = ex.DebugId }, statusCode: StatusCodes.Status502BadGateway);
        }

        var payment = existingPayment ?? new Payment(order.Id, services.PaymentGateway.Currency);
        payment.RecordAuthorization(authorization.PaypalOrderId, invoiceId, authorization.AuthorizationId, authorization.Status, authorization.ExpiresAt, authorization.Amount);

        if (existingPayment is null)
            await services.PaymentRepository.AddAsync(payment);
        else
            await services.PaymentRepository.UpdateAsync(payment);

        order.MarkAuthorized();
        await services.OrderRepository.UpdateAsync(order);

        return Results.Ok(ToResponse(order, payment, services.PaymentGateway.Currency));
    }

    private static PayOrderResponse ToResponse(Order order, Payment payment, string currency) => new()
    {
        OrderId = order.Id,
        Status = order.Status.ToString(),
        AuthorizationId = payment.AuthorizationId,
        AuthorizationStatus = payment.AuthorizationStatus,
        AuthorizedAmount = payment.AuthorizedAmount,
        Currency = currency,
        ExpiresAt = payment.AuthorizationExpiresAt
    };
}
