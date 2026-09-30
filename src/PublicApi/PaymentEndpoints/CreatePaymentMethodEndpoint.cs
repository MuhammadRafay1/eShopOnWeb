using System.Security.Claims;
using System.Text.Json.Serialization;
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

public class CreatePaymentMethodRequest
{
    public CardModel Card { get; set; } = new();
    [JsonIgnore] public string BuyerId { get; set; } = string.Empty;
}

public class CreatePaymentMethodResponse
{
    public int PaymentMethodId { get; set; }
    public string CardBrand { get; set; } = string.Empty;
    public string Last4 { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
}

/// <summary>
/// Saves (vaults) a card for the signed-in shopper. The response identifies the saved card and
/// describes it safely (brand / last four / expiry) — never full card details. Shopper-scoped.
/// </summary>
public class CreatePaymentMethodEndpoint : IEndpoint<IResult, CreatePaymentMethodRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreatePaymentMethodRequest request, ClaimsPrincipal user, IPaymentService paymentService, CancellationToken ct) =>
            {
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, paymentService, ct);
            })
            .Produces<CreatePaymentMethodResponse>(StatusCodes.Status201Created)
            .WithTags("PaymentMethods");
    }

    public Task<IResult> HandleAsync(CreatePaymentMethodRequest request, IPaymentService paymentService)
        => HandleAsync(request, paymentService, CancellationToken.None);

    public async Task<IResult> HandleAsync(CreatePaymentMethodRequest request, IPaymentService paymentService, CancellationToken ct)
    {
        var method = await paymentService.SaveCardAsync(request.BuyerId, request.Card.ToPaymentCard(), ct);
        var response = new CreatePaymentMethodResponse
        {
            PaymentMethodId = method.Id,
            CardBrand = method.CardBrand,
            Last4 = method.Last4,
            Expiry = method.Expiry
        };
        return Results.Created($"api/payment-methods/{method.Id}", response);
    }
}
