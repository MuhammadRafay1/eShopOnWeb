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

public class DeletePaymentMethodRequest
{
    public int PaymentMethodId { get; set; }
    public string BuyerId { get; set; } = string.Empty;

    public DeletePaymentMethodRequest(int paymentMethodId, string buyerId)
    {
        PaymentMethodId = paymentMethodId;
        BuyerId = buyerId;
    }
    public DeletePaymentMethodRequest() { }
}

/// <summary>
/// Removes a saved card. Afterwards it no longer appears among the caller's cards and can no longer
/// be used to pay. Shopper-scoped: a shopper can only delete their own card.
/// </summary>
public class DeletePaymentMethodEndpoint : IEndpoint<IResult, DeletePaymentMethodRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/payment-methods/{paymentMethodId:int}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int paymentMethodId, ClaimsPrincipal user, IPaymentService paymentService, CancellationToken ct) =>
            {
                return await HandleAsync(new DeletePaymentMethodRequest(paymentMethodId, user.Identity!.Name!), paymentService, ct);
            })
            .WithTags("PaymentMethods");
    }

    public Task<IResult> HandleAsync(DeletePaymentMethodRequest request, IPaymentService paymentService)
        => HandleAsync(request, paymentService, CancellationToken.None);

    public async Task<IResult> HandleAsync(DeletePaymentMethodRequest request, IPaymentService paymentService, CancellationToken ct)
    {
        await paymentService.DeleteSavedCardAsync(request.PaymentMethodId, request.BuyerId, ct);
        return Results.NoContent();
    }
}
