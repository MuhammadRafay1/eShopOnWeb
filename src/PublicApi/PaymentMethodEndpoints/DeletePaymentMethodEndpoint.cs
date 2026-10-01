using System.Security.Claims;
using System.Threading;
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
/// DELETE api/payment-methods/{paymentMethodId} — remove a saved card. Ownership is checked before the
/// PayPal vault delete, so one shopper can never delete another's; afterwards the card no longer appears
/// in the caller's list and can no longer be used to pay. Shopper-scoped.
/// </summary>
public class DeletePaymentMethodEndpoint : IEndpoint<IResult, int, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/payment-methods/{paymentMethodId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (int paymentMethodId, ClaimsPrincipal user, IPaymentService service, CancellationToken ct) =>
                await Run(paymentMethodId, user.Identity!.Name!, service, ct))
            .Produces(StatusCodes.Status204NoContent)
            .WithTags("PaymentMethodEndpoints");
    }

    // Interface shim; the real path (with the caller's identity) is the route lambda above.
    public Task<IResult> HandleAsync(int paymentMethodId, IPaymentService service) =>
        Task.FromResult(Results.BadRequest());

    private static async Task<IResult> Run(int paymentMethodId, string buyerId, IPaymentService service, CancellationToken ct)
    {
        await service.DeleteSavedCardAsync(buyerId, paymentMethodId, ct);
        return Results.NoContent();
    }
}
