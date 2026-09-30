using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class DeletePaymentMethodRequest
{
    public int PaymentMethodId { get; set; }
    public string BuyerId { get; set; } = string.Empty;
}

/// <summary>
/// Removes a saved card belonging to the caller. Afterwards it no longer appears in the list
/// and can no longer be used to pay. A PayPal-side 404 (already gone) still counts as success.
/// </summary>
public class DeletePaymentMethodEndpoint : IEndpoint<IResult, DeletePaymentMethodRequest, PaymentMethodEndpointServices>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/payment-methods/{paymentMethodId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int paymentMethodId, ClaimsPrincipal user, PaymentMethodEndpointServices services) =>
            {
                return await HandleAsync(new DeletePaymentMethodRequest { PaymentMethodId = paymentMethodId, BuyerId = user.Identity!.Name! }, services);
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(DeletePaymentMethodRequest request, PaymentMethodEndpointServices services)
    {
        var buyer = await services.BuyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpecification(request.BuyerId));
        var paymentMethod = buyer?.PaymentMethods.FirstOrDefault(p => p.Id == request.PaymentMethodId);
        if (buyer is null || paymentMethod is null)
            return Results.NotFound();

        if (!string.IsNullOrEmpty(paymentMethod.CardId))
            await services.PaymentGateway.DeleteVaultedCardAsync(paymentMethod.CardId);

        buyer.RemovePaymentMethod(request.PaymentMethodId);
        await services.BuyerRepository.UpdateAsync(buyer);

        return Results.NoContent();
    }
}
