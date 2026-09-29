using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>
/// Removes one of the caller's saved cards. The local row is deleted first - which alone satisfies
/// both "no longer appears" and "no longer usable to pay" - then the PayPal vault token is removed
/// best-effort (a failure there is logged, not surfaced, so the delete never depends on PayPal
/// availability).
/// </summary>
public class DeletePaymentMethodEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/payment-methods/{paymentMethodId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                int paymentMethodId,
                IRepository<Buyer> buyerRepository,
                IPayPalVaultGateway vault,
                ILogger<DeletePaymentMethodEndpoint> logger,
                ClaimsPrincipal user,
                CancellationToken ct) =>
            {
                return await HandleAsync(paymentMethodId, buyerRepository, vault, logger, user, ct);
            })
            .WithTags("PaymentMethodEndpoints");
    }

    private static async Task<IResult> HandleAsync(int paymentMethodId,
        IRepository<Buyer> buyerRepository, IPayPalVaultGateway vault,
        ILogger<DeletePaymentMethodEndpoint> logger, ClaimsPrincipal user, CancellationToken ct)
    {
        var buyerId = user.Identity!.Name!;
        var buyer = await buyerRepository.FirstOrDefaultAsync(
            new BuyerWithPaymentMethodsSpecification(buyerId), ct);

        var method = buyer?.PaymentMethods.FirstOrDefault(pm => pm.Id == paymentMethodId);
        if (buyer is null || method is null)
        {
            throw new PaymentMethodNotFoundException(paymentMethodId);
        }

        var vaultId = method.CardId;

        // Delete locally first - this is what makes the card no longer listed or usable to pay.
        buyer.RemovePaymentMethod(paymentMethodId);
        await buyerRepository.UpdateAsync(buyer, ct);

        // Best-effort cleanup at PayPal.
        try
        {
            await vault.DeletePaymentTokenAsync(vaultId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Removed saved card {PaymentMethodId} locally but failed to delete its PayPal vault token; it will need manual cleanup at PayPal.",
                paymentMethodId);
        }

        return Results.NoContent();
    }
}
