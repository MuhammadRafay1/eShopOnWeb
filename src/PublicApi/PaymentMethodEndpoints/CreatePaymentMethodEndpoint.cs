using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.PublicApi.OrderEndpoints;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>
/// POST /api/payment-methods — save a card for the signed-in shopper (vaulted at PayPal).
/// The response identifies the saved card and describes it safely (brand / last four /
/// expiry) — never the full card number. Shopper-scoped.
/// </summary>
public class CreatePaymentMethodEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreatePaymentMethodRequest request, IPaymentService paymentService, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var buyerId = CallerIdentity.BuyerId(user);
                var card = new CardInput(
                    request.Card?.Number ?? string.Empty,
                    request.Card?.ExpiryMonth ?? 0,
                    request.Card?.ExpiryYear ?? 0,
                    request.Card?.Cvv,
                    request.Card?.CardholderName,
                    request.Card?.BillingAddress is null
                        ? null
                        : new PaymentAddressInput(
                            request.Card.BillingAddress.Street, request.Card.BillingAddress.City,
                            request.Card.BillingAddress.State, request.Card.BillingAddress.Country,
                            request.Card.BillingAddress.ZipCode));

                var result = await paymentService.SaveCardAsync(buyerId, card, ct);

                return Results.Created($"api/payment-methods/{result.PaymentMethodId}", new PaymentMethodResponse
                {
                    PaymentMethodId = result.PaymentMethodId,
                    Brand = result.Brand,
                    LastDigits = result.LastDigits,
                    Expiry = result.Expiry,
                    CreatedAt = result.CreatedAt
                });
            })
            .Produces<PaymentMethodResponse>(StatusCodes.Status201Created)
            .WithTags("PaymentMethodEndpoints");
    }
}

public class CreatePaymentMethodRequest
{
    public PayCard? Card { get; set; }
}

public class PaymentMethodResponse
{
    public int PaymentMethodId { get; set; }
    public string? Brand { get; set; }
    public string? LastDigits { get; set; }
    public string? Expiry { get; set; }
    public System.DateTimeOffset CreatedAt { get; set; }
}
