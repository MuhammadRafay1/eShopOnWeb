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

/// <summary>Saves (vaults) a card for the signed-in shopper. The response never carries the card number.</summary>
public class CreatePaymentMethodEndpoint : IEndpoint<IResult, CreatePaymentMethodRequest, IPaymentMethodService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreatePaymentMethodRequest request, ClaimsPrincipal user, IPaymentMethodService service) =>
            {
                return await HandleAsync(CallerIdentity.GetBuyerId(user), request, service);
            })
            .Produces<CreatePaymentMethodResponse>(StatusCodes.Status201Created)
            .WithTags("PaymentMethodEndpoints");
    }

    public Task<IResult> HandleAsync(CreatePaymentMethodRequest request, IPaymentMethodService service)
        => HandleAsync("", request, service);

    public async Task<IResult> HandleAsync(string buyerId, CreatePaymentMethodRequest request, IPaymentMethodService service)
    {
        var card = PaymentMethodMapper.ToCardDetails(request);
        var saved = await service.SaveCardAsync(buyerId, card);

        var response = new CreatePaymentMethodResponse
        {
            PaymentMethodId = saved.Id,
            Brand = saved.CardBrand,
            Last4 = saved.Last4Digits,
            Expiry = saved.Expiry,
        };
        return Results.Created($"api/payment-methods/{saved.Id}", response);
    }
}
