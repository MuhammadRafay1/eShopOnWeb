using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.PublicApi.PaymentModels;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>
/// POST api/payment-methods — save (vault) a card for the signed-in shopper. The response identifies the
/// saved card and describes it safely (brand, last four, expiry) — never full card details. Shopper-scoped.
/// </summary>
public class SavePaymentMethodEndpoint : IEndpoint<IResult, SavePaymentMethodRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (SavePaymentMethodRequest request, ClaimsPrincipal user, IPaymentService service, CancellationToken ct) =>
            {
                request.BuyerId = user.Identity!.Name!;
                return await Run(request, service, ct);
            })
            .Produces(StatusCodes.Status201Created)
            .WithTags("PaymentMethodEndpoints");
    }

    public Task<IResult> HandleAsync(SavePaymentMethodRequest request, IPaymentService service) =>
        Run(request, service, CancellationToken.None);

    private static async Task<IResult> Run(SavePaymentMethodRequest request, IPaymentService service, CancellationToken ct)
    {
        var view = await service.SaveCardAsync(request.BuyerId, request.Card.ToCardDetails(), ct);
        return Results.Created($"api/payment-methods/{view.PaymentMethodId}", new
        {
            paymentMethodId = view.PaymentMethodId,
            brand = view.Brand,
            lastDigits = view.LastDigits,
            expiry = view.Expiry
        });
    }
}
