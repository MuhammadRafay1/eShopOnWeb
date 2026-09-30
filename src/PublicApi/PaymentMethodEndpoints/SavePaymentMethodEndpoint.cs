using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class SavePaymentMethodRequestBody
{
    public CardRequestDto Card { get; init; } = null!;
    public string? Label { get; init; }
}

public class SavePaymentMethodCommand
{
    public CardRequestDto Card { get; init; } = null!;
    public string? Label { get; init; }
}

public class SavePaymentMethodResponse
{
    public int PaymentMethodId { get; init; }
    public string? Brand { get; init; }
    public string? Last4 { get; init; }
    public string? Expiry { get; init; }
    public string? Type { get; init; }
    public string? Label { get; init; }
}

/// <summary>
/// Vaults a card for the signed-in shopper so a later order can be paid without re-entering it.
/// Never returns full card details - only a safe display (brand/last 4/expiry).
/// </summary>
public class SavePaymentMethodEndpoint : IEndpoint<IResult, SavePaymentMethodCommand, string, ISavedCardService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (SavePaymentMethodRequestBody body, ClaimsPrincipal user, ISavedCardService savedCardService) =>
            {
                var command = new SavePaymentMethodCommand { Card = body.Card, Label = body.Label };
                return await HandleAsync(command, user.Identity!.Name!, savedCardService);
            })
            .Produces<SavePaymentMethodResponse>(StatusCodes.Status201Created)
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(SavePaymentMethodCommand request, string buyerId, ISavedCardService savedCardService)
    {
        var cardDetails = request.Card.ToCardDetails();
        var result = await savedCardService.SaveCardAsync(buyerId, cardDetails, request.Label);

        var response = new SavePaymentMethodResponse
        {
            PaymentMethodId = result.PaymentMethodId,
            Brand = result.Brand,
            Last4 = result.Last4,
            Expiry = result.Expiry,
            Type = result.CardType,
            Label = result.Label
        };
        return Results.Created($"api/payment-methods/{response.PaymentMethodId}", response);
    }
}
