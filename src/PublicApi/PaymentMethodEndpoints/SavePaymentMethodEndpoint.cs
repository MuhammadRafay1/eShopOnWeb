using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>
/// Saves a card to PayPal Vault for the signed-in shopper. The response never carries full card
/// details -- only a safe descriptor (brand/last4/expiry) the shopper can recognise the card by.
/// </summary>
public class SavePaymentMethodEndpoint : IEndpoint<IResult, SavePaymentMethodRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SavePaymentMethodRequest request, IPaymentService paymentService, ClaimsPrincipal user) =>
            {
                request.OwnerId = user.Identity!.Name!;
                return await HandleAsync(request, paymentService);
            })
            .Produces<SavePaymentMethodResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(SavePaymentMethodRequest request, IPaymentService paymentService)
    {
        var response = new SavePaymentMethodResponse(request.CorrelationId());

        var card = new CardDetails(
            request.Card.Name,
            request.Card.Number,
            request.Card.Expiry,
            request.Card.SecurityCode,
            new PayPalBillingAddress(
                request.Card.BillingAddress.AddressLine1,
                request.Card.BillingAddress.AddressLine2,
                request.Card.BillingAddress.AdminArea2,
                request.Card.BillingAddress.AdminArea1,
                request.Card.BillingAddress.PostalCode,
                request.Card.BillingAddress.CountryCode));

        var method = await paymentService.SavePaymentMethodAsync(request.OwnerId, card);

        response.PaymentMethodId = method.Id;
        response.PaymentMethod = method.ToDto();

        return Results.Created($"api/payment-methods/{method.Id}", response);
    }
}
