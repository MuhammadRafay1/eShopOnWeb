using System.Collections.Generic;
using System.Linq;
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

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>
/// GET /api/payment-methods — the caller's saved cards (brand / last four / expiry only).
/// Shopper-scoped: only the caller's own cards.
/// </summary>
public class ListPaymentMethodsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IPaymentService paymentService, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var buyerId = CallerIdentity.BuyerId(user);
                var cards = await paymentService.ListCardsAsync(buyerId, ct);
                return Results.Ok(new ListPaymentMethodsResponse
                {
                    PaymentMethods = cards.Select(c => new PaymentMethodResponse
                    {
                        PaymentMethodId = c.PaymentMethodId,
                        Brand = c.Brand,
                        LastDigits = c.LastDigits,
                        Expiry = c.Expiry,
                        CreatedAt = c.CreatedAt
                    }).ToList()
                });
            })
            .Produces<ListPaymentMethodsResponse>()
            .WithTags("PaymentMethodEndpoints");
    }
}

public class ListPaymentMethodsResponse
{
    public List<PaymentMethodResponse> PaymentMethods { get; set; } = new();
}
