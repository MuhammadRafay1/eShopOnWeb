using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>The signed-in shopper's own saved cards.</summary>
public class ListPaymentMethodsEndpoint : IEndpoint<IResult, string, IPaymentMethodService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPaymentMethodService service) =>
            {
                return await HandleAsync(CallerIdentity.GetBuyerId(user), service);
            })
            .Produces<ListPaymentMethodsResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, IPaymentMethodService service)
    {
        var cards = await service.ListAsync(buyerId);
        var response = new ListPaymentMethodsResponse
        {
            PaymentMethods = cards.Select(PaymentMethodMapper.ToDto).ToList(),
        };
        return Results.Ok(response);
    }
}
