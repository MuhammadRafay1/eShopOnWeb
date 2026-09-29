using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.PublicApi.PaymentModels;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>
/// Saves a card for the signed-in shopper by vaulting it at PayPal. The response describes the
/// card safely (brand/last4/expiry) - never the PAN, which is only ever forwarded to PayPal.
/// </summary>
public class CreatePaymentMethodEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                CreatePaymentMethodRequest request,
                IRepository<Buyer> buyerRepository,
                IPayPalVaultGateway vault,
                ClaimsPrincipal user,
                CancellationToken ct) =>
            {
                return await HandleAsync(request, buyerRepository, vault, user, ct);
            })
            .Produces<CreatePaymentMethodResponse>(StatusCodes.Status201Created)
            .WithTags("PaymentMethodEndpoints");
    }

    private static async Task<IResult> HandleAsync(CreatePaymentMethodRequest request,
        IRepository<Buyer> buyerRepository, IPayPalVaultGateway vault, ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (request.Card is null)
        {
            return Results.BadRequest("Card details are required.");
        }

        var buyerId = user.Identity!.Name!;
        var buyer = await buyerRepository.FirstOrDefaultAsync(
            new BuyerWithPaymentMethodsSpecification(buyerId), ct);
        if (buyer is null)
        {
            // Persist first so the buyer has an id for PayPal's caller-owned customer id.
            buyer = await buyerRepository.AddAsync(new Buyer(buyerId), ct);
        }

        var customerId = $"eshop-buyer-{buyer.Id}";
        var saved = await vault.SaveCardAsync(customerId, request.Card.ToCardDetails(), ct);

        var method = buyer.AddPaymentMethod(saved.VaultId, saved.Last4, saved.Brand,
            saved.ExpiryYearMonth, request.Alias);
        await buyerRepository.UpdateAsync(buyer, ct);

        var response = new CreatePaymentMethodResponse
        {
            PaymentMethodId = method.Id,
            Brand = method.Brand,
            Last4 = method.Last4,
            Expiry = method.ExpiryYearMonth,
            Alias = method.Alias
        };
        return Results.Created($"api/payment-methods/{method.Id}", response);
    }
}

public class CreatePaymentMethodRequest
{
    public CardRequestDto? Card { get; set; }
    public string? Alias { get; set; }
}

public class CreatePaymentMethodResponse
{
    public int PaymentMethodId { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Last4 { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
    public string? Alias { get; set; }
}
