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
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.PublicApi.PaymentModels;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>Returns the caller's own saved cards (safe descriptors only). Local DB is the source.</summary>
public class ListPaymentMethodsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                IReadRepository<Buyer> buyerRepository,
                ClaimsPrincipal user,
                CancellationToken ct) =>
            {
                return await HandleAsync(buyerRepository, user, ct);
            })
            .Produces<IReadOnlyList<SavedCardView>>()
            .WithTags("PaymentMethodEndpoints");
    }

    private static async Task<IResult> HandleAsync(IReadRepository<Buyer> buyerRepository,
        ClaimsPrincipal user, CancellationToken ct)
    {
        var buyerId = user.Identity!.Name!;
        var buyer = await buyerRepository.FirstOrDefaultAsync(
            new BuyerWithPaymentMethodsSpecification(buyerId), ct);

        var cards = buyer is null
            ? new List<SavedCardView>()
            : buyer.PaymentMethods.Select(pm => new SavedCardView
            {
                PaymentMethodId = pm.Id,
                Brand = pm.Brand,
                Last4 = pm.Last4,
                Expiry = pm.ExpiryYearMonth,
                Alias = pm.Alias
            }).ToList();

        return Results.Ok(cards);
    }
}
