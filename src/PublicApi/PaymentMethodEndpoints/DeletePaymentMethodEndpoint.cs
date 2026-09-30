using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class DeletePaymentMethodRequest : BaseRequest
{
    public int PaymentMethodId { get; init; }
    [JsonIgnore] public string BuyerId { get; set; } = "";

    public DeletePaymentMethodRequest(int paymentMethodId) => PaymentMethodId = paymentMethodId;
}

/// <summary>
/// DELETE api/payment-methods/{paymentMethodId} — remove a saved card. Deletes at PayPal too, so the
/// card is no longer usable to pay. Shopper-scoped; a shopper can only delete their own.
/// </summary>
public class DeletePaymentMethodEndpoint : IEndpoint<IResult, DeletePaymentMethodRequest, IRepository<PaymentMethod>>
{
    private readonly IPaymentGatewayService _gateway;

    public DeletePaymentMethodEndpoint(IPaymentGatewayService gateway) => _gateway = gateway;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/payment-methods/{paymentMethodId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int paymentMethodId, ClaimsPrincipal user, IRepository<PaymentMethod> repository) =>
            {
                return await HandleAsync(
                    new DeletePaymentMethodRequest(paymentMethodId) { BuyerId = user.Identity!.Name! },
                    repository);
            })
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(DeletePaymentMethodRequest request, IRepository<PaymentMethod> repository)
    {
        var method = await repository.GetByIdAsync(request.PaymentMethodId);
        if (method is null || method.BuyerId != request.BuyerId)
            return Results.NotFound();

        await _gateway.DeleteVaultedCardAsync(method.PayPalVaultId);
        await repository.DeleteAsync(method);

        return Results.NoContent();
    }
}
