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
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>POST /api/payment-methods — save a card for the signed-in shopper. (any authenticated shopper)</summary>
public class CreatePaymentMethodEndpoint : IEndpoint<IResult, CreatePaymentMethodRequest, ISavedCardService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreatePaymentMethodRequest request, ISavedCardService service, ClaimsPrincipal user, CancellationToken ct) =>
            {
                request.CallerId = user.GetBuyerId();
                return await HandleAsync(request, service, ct);
            })
            .Produces<CreatePaymentMethodResponse>(StatusCodes.Status201Created)
            .WithTags("PaymentMethodEndpoints");
    }

    public Task<IResult> HandleAsync(CreatePaymentMethodRequest request, ISavedCardService service) =>
        HandleAsync(request, service, default);

    public async Task<IResult> HandleAsync(CreatePaymentMethodRequest request, ISavedCardService service, CancellationToken ct)
    {
        if (request.Card is null)
        {
            throw new BadPaymentRequestException("Card details are required to save a card.");
        }

        var method = await service.SaveCardAsync(request.CallerId, request.Alias, request.Card.ToCardDetails(), ct);

        var response = new CreatePaymentMethodResponse
        {
            PaymentMethodId = method.Id,
            Alias = method.Alias,
            Last4 = method.Last4,
            Brand = method.Brand,
            Expiry = method.Expiry
        };
        return Results.Created($"api/payment-methods/{method.Id}", response);
    }
}

public class CreatePaymentMethodRequest
{
    public CardInput? Card { get; set; }

    /// <summary>Optional friendly name. Defaults to "{brand} ending {last4}".</summary>
    public string? Alias { get; set; }

    public string CallerId { get; set; } = string.Empty;
}

public class CreatePaymentMethodResponse
{
    public int PaymentMethodId { get; set; }
    public string? Alias { get; set; }
    public string? Last4 { get; set; }
    public string? Brand { get; set; }
    public string? Expiry { get; set; }
}
