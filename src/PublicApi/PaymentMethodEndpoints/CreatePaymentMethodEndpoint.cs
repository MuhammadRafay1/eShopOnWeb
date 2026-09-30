using System;
using System.Security.Claims;
using System.Threading;
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

/// <summary>Saves a card for the signed-in shopper and returns a safe description of it.</summary>
public class CreatePaymentMethodEndpoint : IEndpoint<IResult, SavePaymentMethodRequest, IPaymentMethodService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SavePaymentMethodRequest request, ClaimsPrincipal user, IPaymentMethodService service) =>
            {
                request.BuyerId = CallerIdentity.GetBuyerId(user);
                return await HandleAsync(request, service);
            })
            .Produces<CreatePaymentMethodResponse>(StatusCodes.Status201Created)
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(SavePaymentMethodRequest request, IPaymentMethodService service)
    {
        if (string.IsNullOrEmpty(request.BuyerId))
            return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Number) || string.IsNullOrWhiteSpace(request.Expiry))
            return Results.BadRequest("Card number and expiry (YYYY-MM) are required.");

        PayPalBillingAddress? billing = null;
        if (request.BillingAddress is not null)
        {
            var a = request.BillingAddress;
            billing = new PayPalBillingAddress(a.AddressLine1, a.AddressLine2, a.City, a.State,
                a.PostalCode, a.CountryCode ?? string.Empty);
        }

        var card = new PayPalCard(request.Name ?? string.Empty, request.Number!, request.Expiry!,
            request.Cvv ?? string.Empty, billing);

        var saved = await service.SaveCardAsync(request.BuyerId, card, CancellationToken.None);

        var response = new CreatePaymentMethodResponse(request.CorrelationId())
        {
            PaymentMethodId = saved.PayPalVaultId,
            Brand = saved.Brand,
            Last4 = saved.Last4,
            ExpiryMonth = saved.ExpiryMonth,
            ExpiryYear = saved.ExpiryYear,
            DisplayName = saved.DisplayName
        };
        return Results.Created($"api/payment-methods/{saved.PayPalVaultId}", response);
    }
}

public class CreatePaymentMethodResponse : BaseResponse
{
    public CreatePaymentMethodResponse(Guid correlationId) : base(correlationId) { }
    public CreatePaymentMethodResponse() { }

    /// <summary>Top-level identifier of the saved card, per the task's response-identifier rule.</summary>
    public string PaymentMethodId { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Last4 { get; set; } = string.Empty;
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
