using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Authorizes (holds, does not take) the order total by card — either entered directly, or a saved
/// card the shopper has on file.
/// </summary>
public class PayOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderBody body, ClaimsPrincipal user, IRepository<Order> orderRepository,
             IRepository<Buyer> buyerRepository, IPayPalPaymentGateway gateway, IConfiguration configuration) =>
            {
                var buyerId = user.FindFirstValue(ClaimTypes.Name)!;
                return await HandleAsync(new PayOrderRequest(orderId, buyerId, body), orderRepository, buyerRepository, gateway, configuration);
            })
            .Produces<PayOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(PayOrderRequest request, IRepository<Order> orderRepository,
        IRepository<Buyer> buyerRepository, IPayPalPaymentGateway gateway, IConfiguration configuration)
    {
        var order = await orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdForBuyerSpec(request.OrderId, request.BuyerId));
        if (order is null)
        {
            return Results.NotFound();
        }

        CardDetails? card = null;
        string? savedCardVaultId = null;

        if (request.Body.PaymentMethodId is int paymentMethodId)
        {
            var buyer = await buyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpec(request.BuyerId));
            var paymentMethod = buyer?.PaymentMethods.FirstOrDefault(p => p.Id == paymentMethodId);
            if (paymentMethod is null)
            {
                return Results.NotFound("Saved card not found.");
            }

            savedCardVaultId = paymentMethod.PayPalVaultId;
        }
        else if (request.Body.Card is { } cardRequest)
        {
            if (string.IsNullOrWhiteSpace(cardRequest.Number) || string.IsNullOrWhiteSpace(cardRequest.ExpiryYearMonth) ||
                string.IsNullOrWhiteSpace(cardRequest.SecurityCode))
            {
                return Results.BadRequest("Card number, expiry and security code are required.");
            }

            card = new CardDetails(cardRequest.Number, cardRequest.ExpiryYearMonth, cardRequest.SecurityCode,
                cardRequest.CardholderName, cardRequest.AddressLine1, cardRequest.City, cardRequest.State,
                cardRequest.PostalCode, cardRequest.CountryCode);
        }
        else
        {
            return Results.BadRequest("Either card details or a saved paymentMethodId is required.");
        }

        try
        {
            order.BeginAuthorization(configuration["PayPal:Currency"] ?? "USD", "Order payment");
        }
        catch (System.InvalidOperationException ex)
        {
            return Results.Conflict(ex.Message);
        }

        try
        {
            await orderRepository.UpdateAsync(order);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict("This order is already being paid for.");
        }

        var authorizeRequest = new AuthorizeRequest(order.Id, order.Payment!.Amount, order.Payment.Currency,
            order.Payment.AuthorizeIdempotencyKey, card, savedCardVaultId);

        try
        {
            var result = await gateway.AuthorizeAsync(authorizeRequest, default);

            if (!result.Success)
            {
                order.RecordAuthorizationFailed(result.FailureReason ?? "Payment was declined.");
                await orderRepository.UpdateAsync(order);
                return Results.Json(new { message = result.FailureReason }, statusCode: StatusCodes.Status402PaymentRequired);
            }

            order.RecordAuthorized(result.PayPalOrderId!, result.AuthorizationId!, result.AuthorizationStatus ?? "CREATED");
            await orderRepository.UpdateAsync(order);

            var response = new PayOrderResponse(request.CorrelationId())
            {
                OrderId = order.Id,
                Status = order.Status.ToString(),
                AuthorizationId = order.Payment.AuthorizationId,
                AuthorizationStatus = order.Payment.AuthorizationStatus,
                CardBrand = result.CardBrand,
                CardLast4 = result.CardLast4
            };
            return Results.Ok(response);
        }
        catch (PaymentGatewayException ex)
        {
            order.RecordAuthorizationFailed(ex.Message);
            await orderRepository.UpdateAsync(order);
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
