using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>Saves a card for the signed-in shopper, once, for reuse on a later order.</summary>
public class CreatePaymentMethodEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SaveCardRequestBody card, ClaimsPrincipal user, IRepository<Buyer> buyerRepository, IPayPalPaymentGateway gateway) =>
            {
                var buyerId = user.FindFirstValue(ClaimTypes.Name)!;
                return await HandleAsync(new CreatePaymentMethodRequest(buyerId, card), buyerRepository, gateway);
            })
            .Produces<CreatePaymentMethodResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(CreatePaymentMethodRequest request, IRepository<Buyer> buyerRepository, IPayPalPaymentGateway gateway)
    {
        var card = request.Card;
        if (string.IsNullOrWhiteSpace(card.Number) || string.IsNullOrWhiteSpace(card.ExpiryYearMonth) ||
            string.IsNullOrWhiteSpace(card.SecurityCode))
        {
            return Results.BadRequest("Card number, expiry and security code are required.");
        }

        var cardDetails = new CardDetails(card.Number, card.ExpiryYearMonth, card.SecurityCode, card.CardholderName,
            card.AddressLine1, card.City, card.State, card.PostalCode, card.CountryCode);

        SavedCardResult result;
        try
        {
            result = await gateway.SaveCardAsync(
                new SaveCardRequest(request.BuyerId, System.Guid.NewGuid().ToString(), cardDetails), default);
        }
        catch (PaymentGatewayException ex)
        {
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
        }

        if (!result.Success)
        {
            return Results.Json(new { message = result.FailureReason }, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var buyer = await buyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpec(request.BuyerId));
        if (buyer is null)
        {
            buyer = new Buyer(request.BuyerId);
            await buyerRepository.AddAsync(buyer);
        }

        var paymentMethod = buyer.AddPaymentMethod(result.PayPalVaultId!, result.Brand, result.Last4, result.Expiry);
        await buyerRepository.UpdateAsync(buyer);

        var response = new CreatePaymentMethodResponse(request.CorrelationId())
        {
            PaymentMethodId = paymentMethod.Id,
            Brand = paymentMethod.Brand,
            Last4 = paymentMethod.Last4,
            Expiry = paymentMethod.Expiry,
            Description = paymentMethod.Describe()
        };

        return Results.Created($"api/payment-methods/{paymentMethod.Id}", response);
    }
}
