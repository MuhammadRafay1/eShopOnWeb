using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.PublicApi.PaymentEndpoints;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class SavePaymentMethodRequest : BaseRequest
{
    public CardDto Card { get; set; } = new();
    public string BuyerId { get; set; } = "";
}

public class SavePaymentMethodResponse : BaseResponse
{
    public SavePaymentMethodResponse(Guid correlationId) : base(correlationId) { }

    public int PaymentMethodId { get; set; }
    public string? Brand { get; set; }
    public string? LastFour { get; set; }
    public string? Expiry { get; set; }
}

/// <summary>Saves (vaults) a card for the signed-in shopper. The response describes the card safely — never a PAN.</summary>
public class SavePaymentMethodEndpoint : IEndpoint<IResult, SavePaymentMethodRequest, ISavedPaymentMethodService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SavePaymentMethodRequest request, ClaimsPrincipal user, ISavedPaymentMethodService service,
             CancellationToken ct) =>
            {
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, service, ct);
            })
            .Produces<SavePaymentMethodResponse>(StatusCodes.Status201Created)
            .WithTags("PaymentMethodEndpoints");
    }

    public Task<IResult> HandleAsync(SavePaymentMethodRequest request, ISavedPaymentMethodService service) =>
        HandleAsync(request, service, CancellationToken.None);

    public async Task<IResult> HandleAsync(SavePaymentMethodRequest request, ISavedPaymentMethodService service, CancellationToken ct)
    {
        if (request.Card is null)
        {
            throw new InvalidPaymentRequestException("Card details are required to save a payment method.");
        }

        var saved = await service.SaveCardAsync(request.BuyerId, request.Card.ToCardDetails(), ct);

        var response = new SavePaymentMethodResponse(request.CorrelationId())
        {
            PaymentMethodId = saved.Id,
            Brand = saved.CardBrand,
            LastFour = saved.LastFour,
            Expiry = saved.Expiry
        };
        return Results.Created($"api/payment-methods/{saved.Id}", response);
    }
}
