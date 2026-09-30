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

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

public class ListPaymentMethodsResponse
{
    public List<SavedCardModel> PaymentMethods { get; set; } = new();
}

/// <summary>Lists the caller's own saved cards (brand / last four / expiry / id). Shopper-scoped.</summary>
public class ListPaymentMethodsEndpoint : IEndpoint<IResult, string, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPaymentService paymentService, CancellationToken ct) =>
            {
                return await HandleAsync(user.Identity!.Name!, paymentService, ct);
            })
            .Produces<ListPaymentMethodsResponse>()
            .WithTags("PaymentMethods");
    }

    public Task<IResult> HandleAsync(string buyerId, IPaymentService paymentService)
        => HandleAsync(buyerId, paymentService, CancellationToken.None);

    public async Task<IResult> HandleAsync(string buyerId, IPaymentService paymentService, CancellationToken ct)
    {
        var methods = await paymentService.GetSavedCardsAsync(buyerId, ct);
        var response = new ListPaymentMethodsResponse
        {
            PaymentMethods = methods.Select(m => new SavedCardModel
            {
                PaymentMethodId = m.Id,
                CardBrand = m.CardBrand,
                Last4 = m.Last4,
                Expiry = m.Expiry
            }).ToList()
        };
        return Results.Ok(response);
    }
}
