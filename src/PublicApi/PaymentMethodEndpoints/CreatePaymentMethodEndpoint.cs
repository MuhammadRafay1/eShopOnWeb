using System.Security.Claims;
using System.Threading.Tasks;
using Ardalis.Result;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>Saves a card with PayPal's vault for the signed-in shopper, for reuse on a later order.</summary>
public class CreatePaymentMethodEndpoint : IEndpoint<IResult, CreatePaymentMethodRequest, ISavedCardService>
{
    public void AddRoute(IEndpointRouteBuilder app) =>
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreatePaymentMethodRequest request, ClaimsPrincipal user, ISavedCardService savedCardService) =>
            {
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, savedCardService);
            })
            .Produces<CreatePaymentMethodResponse>()
            .WithTags("PaymentMethodEndpoints");

    public async Task<IResult> HandleAsync(CreatePaymentMethodRequest request, ISavedCardService savedCardService)
    {
        var response = new CreatePaymentMethodResponse(request.CorrelationId());

        var result = await savedCardService.SaveCardAsync(request.BuyerId, request.Card.ToDomain(), request.Label);
        if (result.Status != ResultStatus.Ok)
        {
            return result.ToErrorResult();
        }

        response.PaymentMethodId = result.Value.Id;
        response.PaymentMethod = SavedPaymentMethodDto.FromDomain(result.Value);
        return Results.Created($"api/payment-methods/{result.Value.Id}", response);
    }
}
