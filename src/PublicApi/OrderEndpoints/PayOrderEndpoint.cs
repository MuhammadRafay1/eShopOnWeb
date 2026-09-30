using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// POST /api/orders/{orderId}/pay — authorize (hold) the order total. Carries either raw
/// card details for a one-off payment, or a saved-card paymentMethodId. Idempotent: a
/// repeat call after a successful authorization returns the same hold without re-charging.
/// Shopper-scoped: the caller must own the order.
/// </summary>
public class PayOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, IPaymentService paymentService, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var buyerId = CallerIdentity.BuyerId(user);
                CardInput? card = request.Card is null
                    ? null
                    : new CardInput(
                        request.Card.Number ?? string.Empty,
                        request.Card.ExpiryMonth,
                        request.Card.ExpiryYear,
                        request.Card.Cvv,
                        request.Card.CardholderName,
                        request.Card.BillingAddress is null
                            ? null
                            : new PaymentAddressInput(
                                request.Card.BillingAddress.Street, request.Card.BillingAddress.City,
                                request.Card.BillingAddress.State, request.Card.BillingAddress.Country,
                                request.Card.BillingAddress.ZipCode));

                var result = await paymentService.PayOrderAsync(buyerId, orderId, card, request.PaymentMethodId, ct);

                return Results.Ok(new PayOrderResponse
                {
                    OrderId = result.OrderId,
                    OrderStatus = result.OrderStatus,
                    AuthorizationId = result.AuthorizationId,
                    AuthorizationStatus = result.AuthorizationStatus,
                    AuthorizationExpiresAt = result.AuthorizationExpiresAt,
                    Amount = result.Amount,
                    Currency = result.Currency,
                    AlreadyAuthorized = result.AlreadyAuthorized
                });
            })
            .Produces<PayOrderResponse>()
            .WithTags("OrderEndpoints");
    }
}

public class PayOrderRequest
{
    /// <summary>Raw card for a one-off payment. Mutually exclusive with <see cref="PaymentMethodId"/>.</summary>
    public PayCard? Card { get; set; }

    /// <summary>A saved-card id (from POST /api/payment-methods). Mutually exclusive with <see cref="Card"/>.</summary>
    public int? PaymentMethodId { get; set; }
}

public class PayCard
{
    public string? Number { get; set; }
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public string? Cvv { get; set; }
    public string? CardholderName { get; set; }
    public CreateOrderAddress? BillingAddress { get; set; }
}

public class PayOrderResponse
{
    public int OrderId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string AuthorizationId { get; set; } = string.Empty;
    public string AuthorizationStatus { get; set; } = string.Empty;
    public System.DateTimeOffset? AuthorizationExpiresAt { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool AlreadyAuthorized { get; set; }
}
