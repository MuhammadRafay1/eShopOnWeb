using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class SavePaymentMethodRequest : BaseRequest
{
    public CardDetailsDto Card { get; set; } = new();
}

public class SavePaymentMethodResponse : BaseResponse
{
    public string PaymentMethodId { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string LastDigits { get; set; } = string.Empty;
    public string? Expiry { get; set; }
    public string? CardholderName { get; set; }
}

/// <summary>Saves a card for the signed-in shopper. Never returns full card details - only a safe descriptor.</summary>
public class SavePaymentMethodEndpoint : IEndpoint<IResult, SavePaymentMethodRequest, string, IPaymentMethodService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SavePaymentMethodRequest request, ClaimsPrincipal user, IPaymentMethodService paymentMethodService) =>
            {
                var buyerId = user.FindFirstValue(ClaimTypes.Name)!;
                return await HandleAsync(request, buyerId, paymentMethodService);
            })
            .Produces<SavePaymentMethodResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(SavePaymentMethodRequest request, string buyerId, IPaymentMethodService paymentMethodService)
    {
        var card = new CardDetails(
            request.Card.Number, request.Card.Expiry, request.Card.SecurityCode, request.Card.CardholderName,
            request.Card.Street, request.Card.City, request.Card.State, request.Card.Country, request.Card.PostalCode);

        var saved = await paymentMethodService.SaveAsync(buyerId, card, default);

        var response = new SavePaymentMethodResponse
        {
            PaymentMethodId = saved.PaymentMethodId,
            Brand = saved.Brand,
            LastDigits = saved.LastDigits,
            Expiry = saved.Expiry,
            CardholderName = saved.CardholderName
        };
        return Results.Created($"api/payment-methods/{saved.PaymentMethodId}", response);
    }
}
