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

/// <summary>Removes a saved card. Afterwards it no longer appears for the caller and can no longer be used to pay.</summary>
public class DeletePaymentMethodEndpoint : IEndpoint<IResult, DeletePaymentMethodRequest, ISavedCardService>
{
    public void AddRoute(IEndpointRouteBuilder app) =>
        app.MapDelete("api/payment-methods/{paymentMethodId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int paymentMethodId, ClaimsPrincipal user, ISavedCardService savedCardService) =>
            {
                var request = new DeletePaymentMethodRequest(paymentMethodId) { BuyerId = user.Identity!.Name! };
                return await HandleAsync(request, savedCardService);
            })
            .Produces<DeletePaymentMethodResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("PaymentMethodEndpoints");

    public async Task<IResult> HandleAsync(DeletePaymentMethodRequest request, ISavedCardService savedCardService)
    {
        var result = await savedCardService.DeleteAsync(request.PaymentMethodId, request.BuyerId);
        if (result.Status != ResultStatus.Ok)
        {
            return result.ToErrorResult();
        }

        return Results.Ok(new DeletePaymentMethodResponse(request.CorrelationId()));
    }
}
