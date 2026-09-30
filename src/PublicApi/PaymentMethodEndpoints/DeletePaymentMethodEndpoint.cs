using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>
/// Removes a saved card belonging to the signed-in shopper. Afterwards it no longer appears
/// in the caller's list and can no longer be used to pay.
/// </summary>
public class DeletePaymentMethodEndpoint : IEndpoint<IResult, DeletePaymentMethodRequest, IPaymentService, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/payment-methods/{paymentMethodId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int paymentMethodId, IPaymentService paymentService, HttpContext httpContext) =>
            {
                return await HandleAsync(new DeletePaymentMethodRequest(paymentMethodId), paymentService, httpContext);
            })
            .Produces(204)
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(DeletePaymentMethodRequest request, IPaymentService paymentService, HttpContext httpContext)
    {
        var buyerId = httpContext.User.Identity!.Name!;

        await paymentService.DeletePaymentMethodAsync(request.PaymentMethodId, buyerId, httpContext.RequestAborted);

        return Results.NoContent();
    }
}
