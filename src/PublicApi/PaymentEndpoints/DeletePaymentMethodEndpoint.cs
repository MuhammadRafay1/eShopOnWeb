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

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>DELETE /api/payment-methods/{paymentMethodId} — remove one of the caller's saved cards. (any authenticated shopper)</summary>
public class DeletePaymentMethodEndpoint : IEndpoint<IResult, DeletePaymentMethodRequest, ISavedCardService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/payment-methods/{paymentMethodId:int}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int paymentMethodId, ISavedCardService service, ClaimsPrincipal user, CancellationToken ct) =>
                await HandleAsync(new DeletePaymentMethodRequest(paymentMethodId, user.GetBuyerId()), service, ct))
            .Produces(StatusCodes.Status204NoContent)
            .WithTags("PaymentMethodEndpoints");
    }

    public Task<IResult> HandleAsync(DeletePaymentMethodRequest request, ISavedCardService service) =>
        HandleAsync(request, service, default);

    public async Task<IResult> HandleAsync(DeletePaymentMethodRequest request, ISavedCardService service, CancellationToken ct)
    {
        await service.DeleteAsync(request.CallerId, request.PaymentMethodId, ct);
        return Results.NoContent();
    }
}

public record DeletePaymentMethodRequest(int PaymentMethodId, string CallerId);
