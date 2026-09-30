using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>
/// Saves a card to PayPal's vault for the signed-in shopper. Never returns or stores the PAN
/// or CVV - only a safe descriptor (brand, last 4, expiry).
/// </summary>
public class CreatePaymentMethodEndpoint : IEndpoint<IResult, CreatePaymentMethodRequest, IPaymentService, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreatePaymentMethodRequest request, IPaymentService paymentService, HttpContext httpContext) =>
            {
                return await HandleAsync(request, paymentService, httpContext);
            })
            .Produces<CreatePaymentMethodResponse>()
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(CreatePaymentMethodRequest request, IPaymentService paymentService, HttpContext httpContext)
    {
        var buyerId = httpContext.User.Identity!.Name!;

        var method = await paymentService.SavePaymentMethodAsync(buyerId, request.Card.ToPayPalCardInput(), httpContext.RequestAborted);

        var response = new CreatePaymentMethodResponse(request.CorrelationId())
        {
            PaymentMethodId = method.Id,
            Brand = method.Brand,
            LastFourDigits = method.LastFourDigits,
            ExpiryMonthYear = method.ExpiryMonthYear,
            CardholderName = method.CardholderName
        };
        return Results.Created($"/api/payment-methods/{method.Id}", response);
    }
}
