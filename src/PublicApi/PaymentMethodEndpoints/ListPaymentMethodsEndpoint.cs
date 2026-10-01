using System;
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

/// <summary>The caller's own saved cards.</summary>
public class ListPaymentMethodsEndpoint : IEndpoint<IResult, string, IPaymentMethodService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPaymentMethodService paymentMethodService) =>
            {
                return await HandleAsync(user.Identity!.Name!, paymentMethodService);
            })
            .Produces<ListPaymentMethodsResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, IPaymentMethodService paymentMethodService)
    {
        var paymentMethods = await paymentMethodService.GetForBuyerAsync(buyerId, default);

        var response = new ListPaymentMethodsResponse(Guid.NewGuid())
        {
            PaymentMethods = paymentMethods.Select(PaymentMethodDto.From).ToArray()
        };
        return Results.Ok(response);
    }
}
