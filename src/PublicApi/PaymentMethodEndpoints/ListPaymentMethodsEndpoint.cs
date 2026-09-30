using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>Returns the caller's own saved cards as safe descriptors.</summary>
public class ListPaymentMethodsEndpoint : IEndpoint<IResult, IReadRepository<PaymentMethod>, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IReadRepository<PaymentMethod> paymentMethodRepository, HttpContext httpContext) =>
            {
                return await HandleAsync(paymentMethodRepository, httpContext);
            })
            .Produces<ListPaymentMethodsResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(IReadRepository<PaymentMethod> paymentMethodRepository, HttpContext httpContext)
    {
        var buyerId = httpContext.User.Identity!.Name!;

        var methods = await paymentMethodRepository.ListAsync(new PaymentMethodsByBuyerSpec(buyerId), httpContext.RequestAborted);

        var response = new ListPaymentMethodsResponse
        {
            PaymentMethods = methods.Select(m => new PaymentMethodDto
            {
                PaymentMethodId = m.Id,
                Brand = m.Brand,
                LastFourDigits = m.LastFourDigits,
                ExpiryMonthYear = m.ExpiryMonthYear,
                CardholderName = m.CardholderName,
                CreatedAt = m.CreatedAt
            }).ToList()
        };

        return Results.Ok(response);
    }
}
