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

public class DeletePaymentMethodCommand
{
    public int PaymentMethodId { get; init; }
}

/// <summary>
/// Removes a saved card belonging to the signed-in shopper. Afterwards it no longer lists and
/// can no longer be used to pay.
/// </summary>
public class DeletePaymentMethodEndpoint : IEndpoint<IResult, DeletePaymentMethodCommand, string, ISavedCardService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/payment-methods/{paymentMethodId:int}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (int paymentMethodId, ClaimsPrincipal user, ISavedCardService savedCardService) =>
            {
                return await HandleAsync(new DeletePaymentMethodCommand { PaymentMethodId = paymentMethodId }, user.Identity!.Name!, savedCardService);
            })
            .Produces(StatusCodes.Status204NoContent)
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(DeletePaymentMethodCommand request, string buyerId, ISavedCardService savedCardService)
    {
        await savedCardService.DeleteCardAsync(buyerId, request.PaymentMethodId);
        return Results.NoContent();
    }
}
