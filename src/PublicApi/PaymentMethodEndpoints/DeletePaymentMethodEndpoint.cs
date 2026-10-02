using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>
/// Removes a saved card. Afterwards it no longer appears among the caller's saved cards, and can no
/// longer be used to pay.
/// </summary>
public class DeletePaymentMethodEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/payment-methods/{paymentMethodId:int}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int paymentMethodId, ClaimsPrincipal user, IRepository<Buyer> buyerRepository, IPayPalPaymentGateway gateway) =>
            {
                var buyerId = user.FindFirstValue(ClaimTypes.Name)!;
                return await HandleAsync(paymentMethodId, buyerId, buyerRepository, gateway);
            })
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(int paymentMethodId, string buyerId, IRepository<Buyer> buyerRepository, IPayPalPaymentGateway gateway)
    {
        var buyer = await buyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpec(buyerId));
        var paymentMethod = buyer?.PaymentMethods.FirstOrDefault(p => p.Id == paymentMethodId);
        if (buyer is null || paymentMethod is null)
        {
            return Results.NotFound();
        }

        var vaultId = paymentMethod.PayPalVaultId;

        buyer.RemovePaymentMethod(paymentMethodId);
        await buyerRepository.UpdateAsync(buyer);

        // Best-effort: our own store is the authoritative record of what the shopper can use; see
        // PayPalPaymentGateway.DeleteSavedCardAsync.
        await gateway.DeleteSavedCardAsync(vaultId, default);

        return Results.NoContent();
    }
}
