using System;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;
using AppBillingAddress = Microsoft.eShopWeb.ApplicationCore.Interfaces.BillingAddress;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class SavePaymentMethodBillingAddress
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string CountryCode { get; set; } = "";
}

public class SavePaymentMethodRequest : BaseRequest
{
    public string CardholderName { get; set; } = "";
    public string CardNumber { get; set; } = "";
    public string Expiry { get; set; } = "";        // "YYYY-MM"
    public string SecurityCode { get; set; } = "";
    public SavePaymentMethodBillingAddress? BillingAddress { get; set; }

    [JsonIgnore] public string BuyerId { get; set; } = "";
}

public class SavePaymentMethodResponse : BaseResponse
{
    public SavePaymentMethodResponse(Guid correlationId) : base(correlationId) { }
    public SavePaymentMethodResponse() { }

    public int PaymentMethodId { get; set; }
    public string Brand { get; set; } = "";
    public string LastDigits { get; set; } = "";
    public string Expiry { get; set; } = "";
}

/// <summary>
/// POST api/payment-methods — save a card for the signed-in shopper. Returns a display-safe summary,
/// never full card details or the vault id.
/// </summary>
public class SavePaymentMethodEndpoint : IEndpoint<IResult, SavePaymentMethodRequest, IRepository<PaymentMethod>>
{
    private readonly IPaymentGatewayService _gateway;

    public SavePaymentMethodEndpoint(IPaymentGatewayService gateway) => _gateway = gateway;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SavePaymentMethodRequest request, ClaimsPrincipal user, IRepository<PaymentMethod> repository) =>
            {
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, repository);
            })
            .Produces<SavePaymentMethodResponse>(StatusCodes.Status201Created)
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(SavePaymentMethodRequest request, IRepository<PaymentMethod> repository)
    {
        if (string.IsNullOrWhiteSpace(request.CardNumber) ||
            string.IsNullOrWhiteSpace(request.Expiry) ||
            string.IsNullOrWhiteSpace(request.SecurityCode))
            return Results.BadRequest("cardNumber, expiry and securityCode are required.");

        AppBillingAddress? billing = null;
        if (request.BillingAddress is { } b)
            billing = new AppBillingAddress(b.Line1, b.Line2, b.City, b.State, b.PostalCode, b.CountryCode);

        var card = new CardDetails(request.CardholderName, request.CardNumber, request.Expiry,
            request.SecurityCode, billing);

        var vaulted = await _gateway.VaultCardAsync(card);

        var method = new PaymentMethod(request.BuyerId, vaulted.VaultId, vaulted.Brand,
            vaulted.LastDigits, vaulted.Expiry);
        method = await repository.AddAsync(method);

        return Results.Created($"api/payment-methods/{method.Id}",
            new SavePaymentMethodResponse(request.CorrelationId())
            {
                PaymentMethodId = method.Id,
                Brand = method.Brand,
                LastDigits = method.LastDigits,
                Expiry = method.Expiry
            });
    }
}
